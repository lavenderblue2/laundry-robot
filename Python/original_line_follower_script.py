#!/usr/bin/env python3
"""
Line Following Robot with Obstacle Detection
- Uses camera to detect and follow a line
- Uses ultrasonic sensor to detect obstacles
- Stops when obstacle detected, continues when clear

Runs headless without web interface for maximum performance.
Uses multithreading to keep line detection and distance sensing separate.
"""

import cv2
import numpy as np
import time
import threading
import signal
import sys
import subprocess
from gpiozero import DigitalOutputDevice, DigitalInputDevice

# === MOTOR CONFIGURATION ===
# Back Left Motor (Motor 1)
BL_IN1 = 16
BL_IN2 = 20

# Front Right Motor (Motor 2)
FR_IN1 = 19
FR_IN2 = 26

# Front Left Motor (Motor 3)
FL_IN1 = 5
FL_IN2 = 6

# Back Right Motor (Motor 4)
BR_IN1 = 13
BR_IN2 = 21

# === ULTRASONIC SENSOR CONFIGURATION ===
FRONT_TRIG_PIN = 15
FRONT_ECHO_PIN = 14
REAR_TRIG_PIN = 23  # Change to actual rear sensor pins if needed
REAR_ECHO_PIN = 24  # Change to actual rear sensor pins if needed
OBSTACLE_DISTANCE = 15.0  # Distance in cm to consider an obstacle

# === LINE DETECTION PARAMETERS ===
# PID parameters for line following - reduced for smoother control
Kp = 0.2  # Proportional gain (reduced for less aggressive corrections)
Ki = 0.0  # Integral gain
Kd = 0.05  # Derivative gain (reduced to smooth movements)

# Camera parameters
CAMERA_WIDTH = 320  # Lower resolution for faster processing
CAMERA_HEIGHT = 240

# === PROGRAM CONTROL ===
running = True
obstacle_detected = False
moving_backward = False
front_distance = 100.0
rear_distance = 100.0

# Motor state tracking
current_motor_state = "stopped"
line_detected = False
last_line_pos = None
last_line_time = 0
line_memory_timeout = 3.0  # Increased to 3 seconds for much better recovery
line_lost_counter = 0      # Counter for consecutive frames with lost line
max_line_lost_count = 20   # Increased count before search pattern
last_direction = "forward" # Track last direction to help with recovery


# Logging control
last_log_time = {}  # For throttling repeated log messages
min_log_interval = 2.0  # Minimum seconds between identical log messages

# === ULTRASONIC SENSOR OBJECTS ===
front_trigger = None
front_echo = None
rear_trigger = None
rear_echo = None

# === THREADING LOCKS ===
distance_lock = threading.Lock()
camera_lock = threading.Lock()
motor_lock = threading.Lock()
log_lock = threading.Lock()

def log_message(message, msg_type="INFO", throttle_key=None):
    """Log a message with timestamp and type, with optional throttling"""
    global last_log_time
    
    # Skip logging if this message is being throttled
    if throttle_key is not None:
        with log_lock:
            current_time = time.time()
            if throttle_key in last_log_time:
                # Skip if the same message was logged too recently
                if current_time - last_log_time[throttle_key] < min_log_interval:
                    return
            last_log_time[throttle_key] = current_time
    
    timestamp = time.strftime("%H:%M:%S", time.localtime())
    print(f"[{timestamp}] {msg_type}: {message}")

# Helper function to set pin state using pinctrl
def set_pin(pin, value):
    try:
        state = "dh" if value else "dl"  # dh = drive high, dl = drive low
        subprocess.run(["sudo", "pinctrl", "set", str(pin), state], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception as e:
        log_message(f"Error setting pin {pin}: {e}", "ERROR")
        # Try with GPIO if installed and accessible
        try:
            import RPi.GPIO as GPIO
            GPIO.setmode(GPIO.BCM)  # Use BCM numbering
            GPIO.setup(pin, GPIO.OUT)  # Set as output
            GPIO.output(pin, value)
        except:
            pass

# Setup GPIO pins
def setup_gpio():
    # Set all control pins as outputs with pull-down and initial low state
    all_pins = [FL_IN1, FL_IN2, FR_IN1, FR_IN2, BL_IN1, BL_IN2, BR_IN1, BR_IN2]
    log_message("Setting up GPIO pins...")
    for pin in all_pins:
        try:
            subprocess.run(["sudo", "pinctrl", "set", str(pin), "op", "pd", "dl"], 
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        except Exception as e:
            log_message(f"Error setting up GPIO {pin}: {e}", "ERROR")

# Cleanup GPIO pins
def cleanup_gpio():
    all_pins = [FL_IN1, FL_IN2, FR_IN1, FR_IN2, BL_IN1, BL_IN2, BR_IN1, BR_IN2]
    log_message("Cleaning up GPIO pins...")
    try:
        for pin in all_pins:
            subprocess.run(["sudo", "pinctrl", "set", str(pin), "dl"], 
                         stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    except Exception as e:
        log_message(f"Error during GPIO cleanup: {e}", "ERROR")

def move_forward():
    """Move the robot forward in a straight line"""
    global moving_backward, current_motor_state
    
    with motor_lock:
        if current_motor_state == "forward":
            return  # Already in this state
        
        moving_backward = False
        log_message("Moving forward", throttle_key="move_forward")
        
        # Front Left backward
        set_pin(FL_IN1, 0)
        set_pin(FL_IN2, 1)
        
        # Front Right backward
        set_pin(FR_IN1, 0)
        set_pin(FR_IN2, 1)
        
        # Back Left backward
        set_pin(BL_IN1, 0)
        set_pin(BL_IN2, 1)
        
        # Back Right backward
        set_pin(BR_IN1, 0)
        set_pin(BR_IN2, 1)
        
        current_motor_state = "forward"

def move_backward():
    """Move the robot backward"""
    global moving_backward, current_motor_state
    
    with motor_lock:
        if current_motor_state == "backward":
            return  # Already in this state
        
        moving_backward = True
        log_message("Moving backward", throttle_key="move_backward")
        
        # Front Left forward
        set_pin(FL_IN1, 1)
        set_pin(FL_IN2, 0)
        
        # Front Right forward
        set_pin(FR_IN1, 1)
        set_pin(FR_IN2, 0)
        
        # Back Left forward
        set_pin(BL_IN1, 1)
        set_pin(BL_IN2, 0)
        
        # Back Right forward
        set_pin(BR_IN1, 1)
        set_pin(BR_IN2, 0)
        
        current_motor_state = "backward"

def turn_left():
    """
    Make the robot turn left (rotate in place)
    - Left motors forward
    - Right motors backward
    """
    global current_motor_state
    
    with motor_lock:
        if current_motor_state == "left":
            return  # Already in this state
            
        log_message("Turning left", throttle_key="turn_left")
        
        # Left motors forward
        # Front Left forward
        set_pin(FL_IN1, 1)
        set_pin(FL_IN2, 0)
        
        # Back Left forward
        set_pin(BL_IN1, 1)
        set_pin(BL_IN2, 0)
        
        # Right motors backward
        # Front Right backward
        set_pin(FR_IN1, 0)
        set_pin(FR_IN2, 1)
        
        # Back Right backward
        set_pin(BR_IN1, 0)
        set_pin(BR_IN2, 1)
        
        current_motor_state = "left"

def turn_right():
    """
    Make the robot turn right (rotate in place)
    - Left motors backward
    - Right motors forward
    """
    global current_motor_state
    
    with motor_lock:
        if current_motor_state == "right":
            return  # Already in this state
            
        log_message("Turning right", throttle_key="turn_right")
        
        # Left motors backward
        # Front Left backward
        set_pin(FL_IN1, 0)
        set_pin(FL_IN2, 1)
        
        # Back Left backward
        set_pin(BL_IN1, 0)
        set_pin(BL_IN2, 1)
        
        # Right motors forward
        # Front Right forward
        set_pin(FR_IN1, 1)
        set_pin(FR_IN2, 0)
        
        # Back Right forward
        set_pin(BR_IN1, 1)
        set_pin(BR_IN2, 0)
        
        current_motor_state = "right"

def left_forward():
    """
    Make the robot move forward while veering left
    - All left motors off
    - All right motors on (turning the robot left)
    """
    global moving_backward, current_motor_state
    
    with motor_lock:
        if current_motor_state == "left_forward":
            return  # Already in this state
            
        moving_backward = False
        log_message("Left forward", throttle_key="left_forward")
        
        # All left motors off
        set_pin(FL_IN1, 0)
        set_pin(FL_IN2, 0)
        set_pin(BL_IN1, 0)
        set_pin(BL_IN2, 0)
        
        # All right motors on (backward, which is forward motion)
        set_pin(FR_IN1, 0)
        set_pin(FR_IN2, 1)
        set_pin(BR_IN1, 0)
        set_pin(BR_IN2, 1)
        
        current_motor_state = "left_forward"

def right_forward():
    """
    Make the robot move forward while veering right
    - All right motors off
    - All left motors on (turning the robot right)
    """
    global moving_backward, current_motor_state
    
    with motor_lock:
        if current_motor_state == "right_forward":
            return  # Already in this state
            
        moving_backward = False
        log_message("Right forward", throttle_key="right_forward")
        
        # All right motors off
        set_pin(FR_IN1, 0)
        set_pin(FR_IN2, 0)
        set_pin(BR_IN1, 0)
        set_pin(BR_IN2, 0)
        
        # All left motors on (backward, which is forward motion)
        set_pin(FL_IN1, 0)
        set_pin(FL_IN2, 1)
        set_pin(BL_IN1, 0)
        set_pin(BL_IN2, 1)
        
        current_motor_state = "right_forward"

def stop():
    """Stop all motors"""
    global current_motor_state
    
    with motor_lock:
        if current_motor_state == "stopped":
            return  # Already stopped
            
        log_message("Stopping motors", throttle_key="stop_motors")
        
        # Stop all motors
        set_pin(FL_IN1, 0)
        set_pin(FL_IN2, 0)
        set_pin(FR_IN1, 0)
        set_pin(FR_IN2, 0)
        set_pin(BL_IN1, 0)
        set_pin(BL_IN2, 0)
        set_pin(BR_IN1, 0)
        set_pin(BR_IN2, 0)
        
        current_motor_state = "stopped"

# === ULTRASONIC SENSOR FUNCTIONS ===
def setup_sensors():
    """Initialize GPIO pins for ultrasonic sensors."""
    global front_trigger, front_echo, rear_trigger, rear_echo
    
    try:
        # Initialize front sensor
        front_trigger = DigitalOutputDevice(FRONT_TRIG_PIN, initial_value=False)
        front_echo = DigitalInputDevice(FRONT_ECHO_PIN, pull_up=None, active_state=True)
        
        # Initialize rear sensor
        rear_trigger = DigitalOutputDevice(REAR_TRIG_PIN, initial_value=False)
        rear_echo = DigitalInputDevice(REAR_ECHO_PIN, pull_up=None, active_state=True)
        
        # Allow sensors to settle
        log_message("Ultrasonic sensors initializing...")
        time.sleep(1)
        log_message(f"Front sensor ready on TRIG={FRONT_TRIG_PIN}, ECHO={FRONT_ECHO_PIN}")
        log_message(f"Rear sensor ready on TRIG={REAR_TRIG_PIN}, ECHO={REAR_ECHO_PIN}")
        
        return True
    except Exception as e:
        log_message(f"Error setting up ultrasonic sensors: {e}", "ERROR")
        return False

def get_distance(trigger, echo):
    """
    Trigger an ultrasonic sensor and calculate the distance.
    
    Args:
        trigger: The trigger pin object
        echo: The echo pin object
        
    Returns:
        float: Distance in centimeters, -1 if measurement failed
    """
    if trigger is None or echo is None:
        return -1
    
    try:
        # Send 10us pulse to trigger
        trigger.on()
        time.sleep(0.00001)  # 10 microseconds
        trigger.off()
        
        # Wait for echo to go high (start of pulse)
        start_time = time.time()
        timeout = start_time + 0.03  # 30ms timeout, reduced for faster response
        
        while not echo.is_active and time.time() < timeout:
            start_time = time.time()
            
        if time.time() >= timeout:
            return -1
            
        # Wait for echo to go low (end of pulse)
        end_time = time.time()
        timeout = end_time + 0.03  # 30ms timeout, reduced for faster response
        
        while echo.is_active and time.time() < timeout:
            end_time = time.time()
            
        if time.time() >= timeout:
            return -1
            
        # Calculate distance
        duration = end_time - start_time
        distance = (duration * 34300) / 2  # Speed of sound = 343 m/s
        
        return distance
        
    except Exception as e:
        log_message(f"Error measuring distance: {e}", "ERROR")
        return -1

def distance_sensor_thread():
    """Thread function to continuously read distances from ultrasonic sensors"""
    global front_distance, rear_distance, running, obstacle_detected, moving_backward
    
    # For throttling distance reports
    last_distance_report = 0
    distance_report_interval = 3.0  # seconds
    
    while running:
        try:
            # Determine which sensor to check based on movement direction
            if moving_backward:
                # Check rear sensor when moving backward
                current_distance = get_distance(rear_trigger, rear_echo)
                if current_distance > 0:
                    with distance_lock:
                        rear_distance = current_distance
                        # Update obstacle detection status
                        if rear_distance < OBSTACLE_DISTANCE:
                            if not obstacle_detected:
                                log_message(f"REAR OBSTACLE DETECTED! Distance: {rear_distance:.1f} cm", "WARNING")
                                obstacle_detected = True
                        else:
                            if obstacle_detected:
                                log_message(f"Rear obstacle cleared. Distance: {rear_distance:.1f} cm")
                                obstacle_detected = False
                
                # Occasionally report distance
                current_time = time.time()
                if current_time - last_distance_report > distance_report_interval:
                    log_message(f"Rear distance: {rear_distance:.1f} cm", throttle_key="distance_report")
                    last_distance_report = current_time
                
            else:
                # Check front sensor when moving forward
                current_distance = get_distance(front_trigger, front_echo)
                if current_distance > 0:
                    with distance_lock:
                        front_distance = current_distance
                        # Update obstacle detection status
                        if front_distance < OBSTACLE_DISTANCE:
                            if not obstacle_detected:
                                log_message(f"FRONT OBSTACLE DETECTED! Distance: {front_distance:.1f} cm", "WARNING")
                                obstacle_detected = True
                        else:
                            if obstacle_detected:
                                log_message(f"Front obstacle cleared. Distance: {front_distance:.1f} cm")
                                obstacle_detected = False
                
                # Occasionally report distance
                current_time = time.time()
                if current_time - last_distance_report > distance_report_interval:
                    log_message(f"Front distance: {front_distance:.1f} cm", throttle_key="distance_report")
                    last_distance_report = current_time
            
            # Sleep to avoid overwhelming the CPU and keep sensor reading at ~0.5 second intervals
            time.sleep(0.5)
            
        except Exception as e:
            log_message(f"Error in distance sensor thread: {e}", "ERROR")
            time.sleep(1)  # Delay longer on error

# === CAMERA AND LINE DETECTION FUNCTIONS ===
def initialize_camera():
    """Initialize the camera for line detection"""
    try:
        camera = cv2.VideoCapture(0)  # Use first camera
        
        # Set lower resolution for higher FPS
        camera.set(cv2.CAP_PROP_FRAME_WIDTH, CAMERA_WIDTH)
        camera.set(cv2.CAP_PROP_FRAME_HEIGHT, CAMERA_HEIGHT)
        
        # Try to increase FPS by setting buffer size
        camera.set(cv2.CAP_PROP_BUFFERSIZE, 1)
        
        if camera.isOpened():
            # Try to set higher FPS
            camera.set(cv2.CAP_PROP_FPS, 30)
            actual_width = camera.get(cv2.CAP_PROP_FRAME_WIDTH)
            actual_height = camera.get(cv2.CAP_PROP_FRAME_HEIGHT)
            actual_fps = camera.get(cv2.CAP_PROP_FPS)
            log_message(f"Camera initialized: {actual_width}x{actual_height} @ {actual_fps} FPS")
            return camera
        else:
            log_message("Failed to open camera", "ERROR")
            return None
    except Exception as e:
        log_message(f"Error initializing camera: {e}", "ERROR")
        return None

def process_frame(frame):
    """Process the camera frame to detect the line with enhanced algorithms"""
    try:
        # Use a larger ROI to avoid losing the line - capture more of the frame
        height, width = frame.shape[:2]
        roi = frame[int(height*0.3):height, :]  # Use more of the frame (70% instead of 50%)
        
        # Convert to grayscale
        gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
        
        # Apply basic blur (faster than Gaussian)
        blurred = cv2.blur(gray, (5, 5))
        
        # Try multiple thresholding methods and combine results for more robust detection
        
        # Method 1: Simple binary threshold
        _, thresh1 = cv2.threshold(blurred, 60, 255, cv2.THRESH_BINARY_INV)
        
        # Method 2: Adaptive threshold
        thresh2 = cv2.adaptiveThreshold(blurred, 255, cv2.ADAPTIVE_THRESH_MEAN_C,
                                      cv2.THRESH_BINARY_INV, 11, 7)
        
        # Combine the results (logical OR)
        combined = cv2.bitwise_or(thresh1, thresh2)
        
        # Optional: Apply morphological operations to clean up the result
        kernel = np.ones((5, 5), np.uint8)
        dilated = cv2.dilate(combined, kernel, iterations=1)
        eroded = cv2.erode(dilated, kernel, iterations=1)
        
        return eroded
    except Exception as e:
        log_message(f"Error processing frame: {e}", "ERROR")
        return None

def get_line_position(binary_img):
    """Find the position of the line in the binary image with enhanced recovery"""
    global last_line_pos, last_line_time
    
    try:
        if binary_img is None:
            return None
            
        # Try a more advanced line detection approach
        # First look for contours as before
        height, width = binary_img.shape[:2]
        
        # Find contours in the binary image
        contours, _ = cv2.findContours(binary_img, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        
        # Filter contours by minimum area and aspect ratio to better identify lines
        valid_contours = []
        for c in contours:
            area = cv2.contourArea(c)
            if area < 50:  # Skip very small contours
                continue
                
            # Get bounding rectangle
            x, y, w, h = cv2.boundingRect(c)
            
            # Calculate aspect ratio
            aspect_ratio = float(w) / h if h > 0 else 0
            
            # Lines typically have certain characteristics:
            # - Reasonable size
            # - Either long and thin (high aspect ratio) or blob-like for intersection points
            if (area > 100) or (aspect_ratio > 2.0) or (w > width/5):
                valid_contours.append(c)
        
        if valid_contours:
            # Find the largest contour by area
            largest_contour = max(valid_contours, key=cv2.contourArea)
            
            # Calculate the centroid of the contour
            M = cv2.moments(largest_contour)
            if M["m00"] > 0:  # Ensure we don't divide by zero
                cx = int(M["m10"] / M["m00"])
                
                # Check if this is a reasonable position relative to last position
                # This helps with sudden jumps or false readings
                if last_line_pos is not None:
                    # If the position changes too drastically, ease into the new position
                    if abs(cx - last_line_pos) > 100:
                        # Move halfway toward the new position (smoother transitions)
                        cx = last_line_pos + ((cx - last_line_pos) // 2)
                        log_message(f"Smoothing position change: {last_line_pos}->{cx}", 
                                   throttle_key="position_smoothing")
                
                last_line_pos = cx
                last_line_time = time.time()
                return cx
            
        # If contour method failed, try a simplified sum-based approach
        # This is especially useful for situations where the line is thin or faint
        if not valid_contours:
            # Sum each column to find where the white pixels are concentrated
            column_sums = np.sum(binary_img, axis=0)
            
            # Find columns with significant white pixel count
            threshold = np.max(column_sums) * 0.7  # 70% of maximum value
            white_cols = np.where(column_sums > threshold)[0]
            
            if len(white_cols) > 0:
                # Calculate the centroid of these columns
                centroid = int(np.mean(white_cols))
                
                # Apply similar smoothing as above
                if last_line_pos is not None:
                    if abs(centroid - last_line_pos) > 100:
                        centroid = last_line_pos + ((centroid - last_line_pos) // 2)
                
                log_message("Used column sum method for line detection", throttle_key="column_method")
                last_line_pos = centroid
                last_line_time = time.time()
                return centroid
        
        # Enhanced line memory:
        if last_line_pos is not None:
            current_time = time.time()
            time_since_line = current_time - last_line_time
            
            # Use remembered position with confidence that decreases over time
            if time_since_line < line_memory_timeout:
                # Return the last known position
                return last_line_pos
            
        return None  # No line found
    except Exception as e:
        log_message(f"Error getting line position: {e}", "ERROR")
        return None

def line_detection_thread():
    """Thread function for line detection and following"""
    global running, obstacle_detected, line_detected, last_line_pos, last_line_time
    global line_lost_counter, current_motor_state, last_direction
    
    # PID control variables
    prev_error = 0
    integral = 0
    
    # Initialize camera
    camera = initialize_camera()
    if camera is None:
        log_message("Failed to initialize camera! Exiting line detection thread.", "ERROR")
        return
    
    log_message("Line detection started...")
    frames_processed = 0
    start_time = time.time()
    last_fps_report_time = start_time
    
    try:
        while running:
            loop_start = time.time()
            
            # Check if obstacle detected
            with distance_lock:
                local_obstacle_detected = obstacle_detected
            
            if local_obstacle_detected:
                # If obstacle detected, stop the robot
                stop()
                # Don't process frames too frequently when stopped
                time.sleep(0.1)
                continue
            
            # Capture frame from camera
            with camera_lock:
                ret, frame = camera.read()
            
            if not ret:
                log_message("Failed to capture frame from camera", "ERROR", throttle_key="camera_fail")
                time.sleep(0.1)
                continue
            
            # Process the frame to detect the line
            binary = process_frame(frame)
            line_pos = get_line_position(binary)
            
            # Calculate FPS occasionally
            frames_processed += 1
            current_time = time.time()
            if current_time - last_fps_report_time >= 5.0:  # Report every 5 seconds
                fps = frames_processed / (current_time - last_fps_report_time)
                log_message(f"Line detection running at {fps:.1f} FPS")
                frames_processed = 0
                last_fps_report_time = current_time
            
            if line_pos is not None:
                # Line detected - reset the line lost counter
                line_lost_counter = 0
                
                # Update line detected status if needed
                if not line_detected:
                    log_message("Line detected")
                    line_detected = True
                
                # Calculate the center of the frame
                frame_center = binary.shape[1] // 2
                
                # Calculate error (distance from line to center)
                error = frame_center - line_pos
                
                # PID control computation
                integral = max(-100, min(100, integral + error))  # Prevent integral windup
                derivative = error - prev_error
                output = Kp * error + Ki * integral + Kd * derivative
                prev_error = error
                
                # Create a smoother control system with larger thresholds
                # and more emphasis on going straight when possible
                
                # Log the error and output for debugging
                if frames_processed % 20 == 0:  # Only log occasionally
                    log_message(f"Line error: {error}, PID output: {output:.2f}", throttle_key="pid_values")
                
                # Track the last direction for line recovery
                if error > 0:
                    last_direction = "left"
                elif error < 0:
                    last_direction = "right"
                
                # Implement a state machine for smoother motion:
                # 1. If we're close to centered, go straight
                # 2. For small-medium errors, use gentle turns
                # 3. Only use sharp turns for large errors
                
                if abs(error) < 30:  # Very small error - go straight
                    move_forward()
                elif abs(error) < 80:  # Small-medium error - gentle correction
                    if error > 0:
                        left_forward()
                    else:
                        right_forward()
                else:  # Large error - stronger correction but not full turn
                    if error > 0:
                        if error > 150:  # Extreme error - use full turn
                            turn_left()
                        else:
                            left_forward()
                    else:
                        if error < -150:  # Extreme error - use full turn
                            turn_right()
                        else:
                            right_forward()
            else:
                # No line detected
                if line_detected:
                    log_message("Line lost", throttle_key="line_lost")
                    line_detected = False
                
                # Increment the line lost counter
                line_lost_counter += 1
                
                # Try to recover the line with a smarter strategy:
                # 1. First, use recent memory to keep going in the same direction
                # 2. If that fails, implement a more robust search pattern
                
                # Check if we still have a recent memory of line position
                if last_line_pos is not None and (time.time() - last_line_time) <= line_memory_timeout:
                    # We have a recent memory - continue in the same direction
                    log_message(f"Using memory to continue: {time.time() - last_line_time:.1f}s ago", 
                              throttle_key="line_memory")
                    
                    # Determine which side of the frame the line was last seen
                    frame_center = binary.shape[1] // 2
                    
                    # Bias the movement based on where the line was last seen
                    if last_line_pos < frame_center - 50:
                        # Line was on the left side
                        left_forward()
                    elif last_line_pos > frame_center + 50:
                        # Line was on the right side
                        right_forward()
                    else:
                        # Line was near center
                        move_forward()
                    
                elif line_lost_counter >= max_line_lost_count:
                    # We've lost the line for many frames - implement a search pattern
                    line_lost_counter = max_line_lost_count  # Cap the counter
                    
                    # Use our last known direction to help with the search
                    if last_direction == "left":
                        log_message("Line search: sweeping right to find line", throttle_key="line_search")
                        right_forward()
                    elif last_direction == "right":
                        log_message("Line search: sweeping left to find line", throttle_key="line_search")
                        left_forward()
                    else:
                        # If we're truly lost, do a slow 360 scan
                        if line_lost_counter % 40 < 20:  # Alternate direction every 20 frames
                            log_message("Line search: slow right turn", throttle_key="line_search_rotate")
                            # Slow rotation for searching
                            with motor_lock:
                                # Stop other motors
                                set_pin(FL_IN1, 0)
                                set_pin(FL_IN2, 0)
                                set_pin(BL_IN1, 0)
                                set_pin(BL_IN2, 0)
                                # Just power right motors
                                set_pin(FR_IN1, 0)
                                set_pin(FR_IN2, 1)
                                set_pin(BR_IN1, 0)
                                set_pin(BR_IN2, 1)
                                current_motor_state = "search_right"
                        else:
                            log_message("Line search: slow left turn", throttle_key="line_search_rotate")
                            # Slow rotation for searching
                            with motor_lock:
                                # Stop right motors
                                set_pin(FR_IN1, 0)
                                set_pin(FR_IN2, 0)
                                set_pin(BR_IN1, 0)
                                set_pin(BR_IN2, 0)
                                # Just power left motors
                                set_pin(FL_IN1, 0)
                                set_pin(FL_IN2, 1)
                                set_pin(BL_IN1, 0)
                                set_pin(BL_IN2, 1)
                                current_motor_state = "search_left"
                else:
                    # We've only lost the line briefly, continue in the last direction
                    log_message(f"Brief line loss, continuing in last direction: {last_direction}", 
                              throttle_key="brief_line_loss")
                    
                    if last_direction == "left":
                        left_forward()
                    elif last_direction == "right":
                        right_forward()
                    else:
                        move_forward()
                        
                    # Sleep a bit longer to give more time for recovery
                    time.sleep(0.05)  # Additional delay
            
            # Add a brief pause to allow the robot to move between readings
            # This helps create smoother movement by not constantly changing directions
            processing_time = time.time() - loop_start
            
            # Add a motor state-based delay:
            # - Longer delay when going straight (let it move forward)
            # - Shorter delay during turns (more responsive corrections)
            if current_motor_state == "forward":
                target_delay = 0.08  # Longer delay when going straight
            elif current_motor_state in ["left", "right"]:
                target_delay = 0.03  # Shorter delay during sharp turns
            else:
                target_delay = 0.05  # Medium delay for other states
                
            sleep_time = max(0.01, target_delay - processing_time)
            time.sleep(sleep_time)
    
    except Exception as e:
        log_message(f"Error in line detection thread: {e}", "ERROR")
    finally:
        # Release the camera
        if camera is not None:
            camera.release()
        log_message("Line detection stopped")

def signal_handler(sig, frame):
    """Handle Ctrl+C and other signals for graceful shutdown."""
    global running
    
    log_message("\nShutting down...", "INFO")
    running = False  # Stop threads
    time.sleep(1)  # Allow threads to exit
    stop()  # Stop motors
    
    # Clean up resources
    cleanup_gpio()
    
    if front_trigger is not None:
        front_trigger.close()
    if front_echo is not None:
        front_echo.close()
    if rear_trigger is not None:
        rear_trigger.close()
    if rear_echo is not None:
        rear_echo.close()
    
    log_message("Shutdown complete")
    sys.exit(0)

if __name__ == "__main__":
    # Register signal handler for clean shutdown
    signal.signal(signal.SIGINT, signal_handler)   # Handle Ctrl+C
    signal.signal(signal.SIGTERM, signal_handler)  # Handle termination signal
    
    try:
        # Initialize GPIO
        setup_gpio()
        
        # Initialize ultrasonic sensors
        if not setup_sensors():
            log_message("Warning: Failed to initialize ultrasonic sensors. Obstacle detection will not work.", "WARNING")
        
        # Start with motors stopped
        stop()
        
        log_message("\n" + "=" * 50)
        log_message("Line Following Robot with Obstacle Detection")
        log_message("=" * 50)
        log_message("- Following black line with camera")
        log_message(f"- Stopping when obstacles detected within {OBSTACLE_DISTANCE} cm")
        log_message("- Press Ctrl+C to stop the robot")
        log_message("=" * 50 + "\n")
        
        # Start ultrasonic sensor thread
        distance_thread = threading.Thread(target=distance_sensor_thread, daemon=True)
        distance_thread.start()
        
        # Start line detection thread
        line_thread = threading.Thread(target=line_detection_thread, daemon=True)
        line_thread.start()
        
        # Keep main thread alive
        while running:
            time.sleep(0.1)
        
    except Exception as e:
        log_message(f"Error in main thread: {e}", "ERROR")
    finally:
        # Make sure to clean up on exit
        running = False
        stop()
        cleanup_gpio()