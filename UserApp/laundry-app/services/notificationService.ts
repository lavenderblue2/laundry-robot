import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';

// SIMPLE notification handler - NO SOUNDS, NO VIBRATIONS, NO BADGE
// This prevents crashes by keeping it minimal
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: false,
    shouldSetBadge: false,
  }),
});

class NotificationService {
  private isInitialized = false;

  /**
   * Initialize notification service
   * Sets up Android channels (required for Android 8+)
   */
  async initialize() {
    if (this.isInitialized) return;

    try {
      // Set up notification channel for Android
      if (Platform.OS === 'android') {
        await this.setupAndroidChannel();
      }

      this.isInitialized = true;
      console.log('✅ Notification service initialized');
    } catch (error) {
      console.error('❌ Failed to initialize notifications:', error);
    }
  }

  /**
   * Setup Android notification channel
   * SILENT channel - no sound, no vibration
   * HIGH importance so it shows as popup
   */
  private async setupAndroidChannel() {
    await Notifications.setNotificationChannelAsync('default', {
      name: 'Default',
      importance: Notifications.AndroidImportance.HIGH,
      sound: null, // No sound
      enableVibrate: false, // No vibration
      vibrationPattern: [], // No vibration pattern
      showBadge: false, // No badge
    });
    console.log('✅ Android notification channel created');
  }

  /**
   * Request notification permissions
   * Returns true if granted, false otherwise
   */
  async requestPermissions(): Promise<boolean> {
    try {
      const { status: existingStatus } = await Notifications.getPermissionsAsync();
      let finalStatus = existingStatus;

      // Request permission if not already granted
      if (existingStatus !== 'granted') {
        const { status } = await Notifications.requestPermissionsAsync();
        finalStatus = status;
      }

      const granted = finalStatus === 'granted';
      console.log(`📢 Notification permission: ${granted ? 'GRANTED' : 'DENIED'}`);
      return granted;
    } catch (error) {
      console.error('❌ Failed to request permissions:', error);
      return false;
    }
  }

  /**
   * Check if permissions are granted
   */
  async hasPermissions(): Promise<boolean> {
    try {
      const { status } = await Notifications.getPermissionsAsync();
      return status === 'granted';
    } catch (error) {
      console.error('❌ Failed to check permissions:', error);
      return false;
    }
  }

  /**
   * Send a simple test notification
   * Shows notification OUTSIDE the app
   */
  async sendTestNotification(): Promise<void> {
    try {
      // Check permissions first
      const hasPermission = await this.hasPermissions();
      if (!hasPermission) {
        throw new Error('Notification permission not granted');
      }

      // Schedule a notification immediately
      await Notifications.scheduleNotificationAsync({
        content: {
          title: '🧺 Test Notification',
          body: 'This is a test notification from your Laundry App!',
        },
        trigger: null, // Show immediately
      });

      console.log('✅ Test notification sent');
    } catch (error) {
      console.error('❌ Failed to send test notification:', error);
      throw error;
    }
  }

  /**
   * Send status change notification
   * Shows notification for important laundry status updates
   */
  async sendStatusNotification(
    status: string,
    requestId: number,
    additionalData?: { weight?: number; totalCost?: number }
  ): Promise<void> {
    try {
      // Check permissions first
      const hasPermission = await this.hasPermissions();
      if (!hasPermission) {
        console.log('⚠️ Notification permission not granted - skipping notification');
        return;
      }

      // Get notification content based on status
      const notificationContent = this.getNotificationContent(status, requestId, additionalData);

      // Only send if this is a critical status
      if (!notificationContent) {
        console.log(`ℹ️ Status "${status}" is not configured for notifications - skipping`);
        return;
      }

      // Schedule notification immediately
      await Notifications.scheduleNotificationAsync({
        content: notificationContent,
        trigger: null, // Show immediately
      });

      console.log(`✅ Notification sent for status: ${status}`);
    } catch (error) {
      console.error(`❌ Failed to send notification for status "${status}":`, error);
      // Don't throw - notifications should be non-blocking
    }
  }

  /**
   * Get notification title and body for a given status
   * Returns null for non-critical statuses
   */
  private getNotificationContent(
    status: string,
    requestId: number,
    additionalData?: { weight?: number; totalCost?: number }
  ): { title: string; body: string } | null {
    const statusLower = status.toLowerCase();

    // Map status to notification content
    switch (statusLower) {
      case 'accepted':
        return {
          title: '✅ Request Approved',
          body: `Your laundry request #${requestId} has been approved! Robot will be dispatched soon.`
        };

      case 'robotenroute':
        return {
          title: '🤖 Robot On The Way',
          body: `Robot is heading to your room for request #${requestId}. Please be ready!`
        };

      case 'arrivedatroom':
        return {
          title: '📍 Robot Has Arrived!',
          body: `The robot is at your door for request #${requestId}. Please load your laundry.`
        };

      case 'laundryloaded':
        return {
          title: '📦 Laundry Picked Up',
          body: `Your laundry has been loaded! Robot is returning to base for request #${requestId}.`
        };

      case 'weighingcomplete':
        const weightMsg = additionalData?.weight
          ? ` Weight: ${additionalData.weight}kg`
          : '';
        return {
          title: '⚖️ Weighing Complete',
          body: `Your laundry has been weighed for request #${requestId}.${weightMsg}`
        };

      case 'paymentpending':
        const costMsg = additionalData?.totalCost
          ? ` Amount: ₱${additionalData.totalCost.toFixed(2)}`
          : '';
        return {
          title: '💳 Payment Required',
          body: `Please complete payment for request #${requestId}.${costMsg}`
        };

      case 'washing':
        return {
          title: '🌊 Washing In Progress',
          body: `Your laundry is now being washed for request #${requestId}.`
        };

      case 'finishedwashing':
        return {
          title: '✨ Washing Complete!',
          body: `Your laundry is clean and ready for request #${requestId}! Choose delivery or pickup.`
        };

      case 'finishedwashinggoingtoroom':
        return {
          title: '🚚 Clean Laundry Delivery',
          body: `Robot is delivering your clean laundry for request #${requestId}. Please be ready!`
        };

      case 'finishedwashingarrivedatroom':
        return {
          title: '📍 Delivery Arrived!',
          body: `Your clean laundry has arrived for request #${requestId}. Please retrieve it from the robot.`
        };

      case 'completed':
        return {
          title: '🎉 Service Complete',
          body: `Your laundry service #${requestId} is complete. Thank you!`
        };

      case 'declined':
        return {
          title: '❌ Request Declined',
          body: `Sorry, your laundry request #${requestId} has been declined by the admin.`
        };

      case 'cancelled':
        return {
          title: '🚫 Request Cancelled',
          body: `Your laundry request #${requestId} has been cancelled.`
        };

      // Non-critical statuses - no notification
      default:
        return null;
    }
  }
}

// Export singleton instance
export const notificationService = new NotificationService();
