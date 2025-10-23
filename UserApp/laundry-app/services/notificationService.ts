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
}

// Export singleton instance
export const notificationService = new NotificationService();
