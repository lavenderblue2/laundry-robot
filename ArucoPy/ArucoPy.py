from flask import Flask, request, jsonify
import cv2
import numpy as np
import os
from datetime import datetime
from colorama import Fore, Back, Style, init

# Initialize colorama
init(autoreset=True)

app = Flask(__name__)

# Initialize ArUco dictionary and parameters
ARUCO_DICT = cv2.aruco.DICT_4X4_50
aruco_dictionary = cv2.aruco.getPredefinedDictionary(ARUCO_DICT)
aruco_parameters = cv2.aruco.DetectorParameters()
detector = cv2.aruco.ArucoDetector(aruco_dictionary, aruco_parameters)

# Create logs directory if it doesn't exist
LOGS_DIR = './logs'
if not os.path.exists(LOGS_DIR):
    os.makedirs(LOGS_DIR)
    print(f"{Fore.GREEN}[INFO] Created logs directory: {LOGS_DIR}")

def log_info(message):
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"{Fore.CYAN}[INFO] {timestamp} - {message}{Style.RESET_ALL}")

def log_success(message):
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"{Fore.GREEN}[SUCCESS] {timestamp} - {message}{Style.RESET_ALL}")

def log_warning(message):
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"{Fore.YELLOW}[WARNING] {timestamp} - {message}{Style.RESET_ALL}")

def log_error(message):
    timestamp = datetime.now().strftime('%Y-%m-%d %H:%M:%S')
    print(f"{Fore.RED}[ERROR] {timestamp} - {message}{Style.RESET_ALL}")

def save_detection_image(image, corners, ids):
    """Draw markers on image and save to logs directory"""
    try:
        # Create a copy of the image to draw on
        output_image = image.copy()
        
        # Draw detected markers
        if ids is not None and len(ids) > 0:
            cv2.aruco.drawDetectedMarkers(output_image, corners, ids)
            
            # Draw additional info for each marker
            for i, marker_id in enumerate(ids):
                # Get corner coordinates
                corner = corners[i][0]
                top_left = tuple(corner[0].astype(int))
                
                # Put marker ID text
                cv2.putText(output_image, f"ID: {marker_id[0]}", 
                           (top_left[0], top_left[1] - 10),
                           cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 0), 2)
        
        # Generate filename with timestamp
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S_%f')
        filename = f"detection_{timestamp}.png"
        filepath = os.path.join(LOGS_DIR, filename)
        
        # Save image
        cv2.imwrite(filepath, output_image)
        log_success(f"Detection image saved: {filename}")
        
        return filename
    except Exception as e:
        log_error(f"Failed to save detection image: {str(e)}")
        return None

@app.route('/detect', methods=['POST'])
def detect_aruco():
    try:
        log_info("Received detection request")
        
        # Check if image file is in request
        if 'image' not in request.files:
            log_warning("No image file provided in request")
            return jsonify({'error': 'No image file provided'}), 400
        
        file = request.files['image']
        log_info(f"Processing image: {file.filename}")
        
        # Read image file
        file_bytes = np.frombuffer(file.read(), np.uint8)
        image = cv2.imdecode(file_bytes, cv2.IMREAD_COLOR)
        
        if image is None:
            log_error("Invalid image file - could not decode")
            return jsonify({'error': 'Invalid image file'}), 400
        
        log_info(f"Image loaded: {image.shape[1]}x{image.shape[0]} pixels")
        
        # Convert to grayscale for detection
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
        
        # Detect ArUco markers
        log_info("Detecting ArUco markers...")
        corners, ids, rejected = detector.detectMarkers(gray)
        
        # Prepare response
        result = {
            'markers_detected': 0,
            'markers': []
        }
        
        if ids is not None:
            result['markers_detected'] = len(ids)
            log_success(f"Detected {len(ids)} ArUco marker(s)")
            
            for i, marker_id in enumerate(ids):
                marker_corners = corners[i][0].tolist()
                result['markers'].append({
                    'id': int(marker_id[0]),
                    'corners': marker_corners
                })
                log_info(f"  - Marker ID {marker_id[0]} at corners: {[f'({c[0]:.1f},{c[1]:.1f})' for c in marker_corners]}")
        else:
            log_warning("No ArUco markers detected in image")
        
        # Check if logging is enabled via query parameter
        log_param = request.args.get('log', 'false').lower()
        if log_param == 'true':
            log_info("Logging enabled - saving detection image")
            saved_filename = save_detection_image(image, corners, ids)
            if saved_filename:
                result['saved_image'] = saved_filename
        
        return jsonify(result), 200
        
    except Exception as e:
        log_error(f"Exception occurred: {str(e)}")
        return jsonify({'error': str(e)}), 500

@app.route('/health', methods=['GET'])
def health_check():
    log_info("Health check requested")
    return jsonify({'status': 'running', 'dictionary': 'DICT_4X4_50'}), 200

if __name__ == '__main__':
    print(f"{Fore.MAGENTA}{Style.BRIGHT}")
    print("=" * 50)
    print("        ArucoPy Detection Server")
    print("=" * 50)
    print(f"{Style.RESET_ALL}")
    log_success("Server initializing...")
    log_info(f"Listening on http://0.0.0.0:25660")
    log_info(f"ArUco Dictionary: DICT_4X4_50")
    log_info(f"Logs directory: {os.path.abspath(LOGS_DIR)}")
    print(f"{Fore.MAGENTA}{'=' * 50}{Style.RESET_ALL}\n")
    
    app.run(host='0.0.0.0', port=25660, debug=False)