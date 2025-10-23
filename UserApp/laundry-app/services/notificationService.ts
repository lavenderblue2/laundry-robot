import * as Notifications from 'expo-notifications';
import { Platform } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';

// Configure notification behavior
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: true,
  }),
});

export interface NotificationSettings {
  notificationsEnabled: boolean;
  vibrationEnabled: boolean;
  robotArrivalEnabled: boolean;
  robotDeliveryEnabled: boolean;
  messagesEnabled: boolean;
  statusChangesEnabled: boolean;
  robotArrivalSound: string;
  messageSound: string;
}

const DEFAULT_SETTINGS: NotificationSettings = {
  notificationsEnabled: true,
  vibrationEnabled: true,
  robotArrivalEnabled: true,
  robotDeliveryEnabled: true,
  messagesEnabled: true,
  statusChangesEnabled: true,
  robotArrivalSound: 'default',
  messageSound: 'default',
};

const SETTINGS_KEY = '@notification_settings';

class NotificationService {
  private settings: NotificationSettings = DEFAULT_SETTINGS;

  async initialize() {
    // Load settings from storage
    await this.loadSettings();

    // Set up notification channels for Android
    if (Platform.OS === 'android') {
      await this.setupAndroidChannels();
    }
  }

  private async setupAndroidChannels() {
    // Robot arrival channel
    await Notifications.setNotificationChannelAsync('robot-arrival', {
      name: 'Robot Arrival',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern: [0, 250, 250, 250],
      sound: 'default',
      enableVibrate: true,
    });

    // Messages channel
    await Notifications.setNotificationChannelAsync('messages', {
      name: 'Messages',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern: [0, 250, 250, 250],
      sound: 'default',
      enableVibrate: true,
    });

    // Status changes channel
    await Notifications.setNotificationChannelAsync('status', {
      name: 'Status Updates',
      importance: Notifications.AndroidImportance.DEFAULT,
      vibrationPattern: [0, 250],
      sound: 'default',
      enableVibrate: true,
    });
  }

  async requestPermissions(): Promise<boolean> {
    const { status: existingStatus } = await Notifications.getPermissionsAsync();
    let finalStatus = existingStatus;

    if (existingStatus !== 'granted') {
      const { status } = await Notifications.requestPermissionsAsync();
      finalStatus = status;
    }

    return finalStatus === 'granted';
  }

  async loadSettings(): Promise<NotificationSettings> {
    try {
      const stored = await AsyncStorage.getItem(SETTINGS_KEY);
      if (stored) {
        this.settings = JSON.parse(stored);
      } else {
        this.settings = DEFAULT_SETTINGS;
        await this.saveSettings(this.settings);
      }
      return this.settings;
    } catch (error) {
      console.error('Error loading notification settings:', error);
      return DEFAULT_SETTINGS;
    }
  }

  async saveSettings(settings: NotificationSettings): Promise<void> {
    try {
      this.settings = settings;
      await AsyncStorage.setItem(SETTINGS_KEY, JSON.stringify(settings));

      // Update Android channels based on settings
      if (Platform.OS === 'android') {
        await this.updateAndroidChannels();
      }
    } catch (error) {
      console.error('Error saving notification settings:', error);
    }
  }

  private async updateAndroidChannels() {
    const vibrationPattern = this.settings.vibrationEnabled ? [0, 250, 250, 250] : undefined;

    await Notifications.setNotificationChannelAsync('robot-arrival', {
      name: 'Robot Arrival',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern,
      sound: this.settings.robotArrivalSound === 'default' ? 'default' : this.settings.robotArrivalSound,
      enableVibrate: this.settings.vibrationEnabled,
    });

    await Notifications.setNotificationChannelAsync('messages', {
      name: 'Messages',
      importance: Notifications.AndroidImportance.HIGH,
      vibrationPattern,
      sound: this.settings.messageSound === 'default' ? 'default' : this.settings.messageSound,
      enableVibrate: this.settings.vibrationEnabled,
    });
  }

  getSettings(): NotificationSettings {
    return { ...this.settings };
  }

  async sendRobotArrivalNotification(location: string, isPickup: boolean = true): Promise<void> {
    if (!this.settings.notificationsEnabled) return;
    if (isPickup && !this.settings.robotArrivalEnabled) return;
    if (!isPickup && !this.settings.robotDeliveryEnabled) return;

    const title = isPickup ? '🤖 Robot Arrived for Pickup!' : '🤖 Your Laundry is Here!';
    const body = isPickup
      ? `The robot has arrived at ${location} to collect your laundry.`
      : `The robot has delivered your clean laundry to ${location}.`;

    await Notifications.scheduleNotificationAsync({
      content: {
        title,
        body,
        sound: this.settings.robotArrivalSound !== 'default' ? this.settings.robotArrivalSound : undefined,
        data: { type: isPickup ? 'robot-pickup' : 'robot-delivery', location },
      },
      trigger: Platform.OS === 'android' ? { channelId: 'robot-arrival' } : null,
    });
  }

  async sendMessageNotification(senderName: string, message: string): Promise<void> {
    if (!this.settings.notificationsEnabled) return;
    if (!this.settings.messagesEnabled) return;

    await Notifications.scheduleNotificationAsync({
      content: {
        title: `💬 New message from ${senderName}`,
        body: message.length > 100 ? message.substring(0, 100) + '...' : message,
        sound: this.settings.messageSound !== 'default' ? this.settings.messageSound : undefined,
        data: { type: 'message', senderName },
      },
      trigger: Platform.OS === 'android' ? { channelId: 'messages' } : null,
    });
  }

  async sendStatusChangeNotification(status: string, details?: string): Promise<void> {
    if (!this.settings.notificationsEnabled) return;
    if (!this.settings.statusChangesEnabled) return;

    const statusMessages: Record<string, string> = {
      'Washing': '🧺 Your laundry is being washed',
      'FinishedWashing': '✨ Washing complete!',
      'FinishedWashingGoingToRoom': '🚀 Robot is delivering your laundry',
      'FinishedWashingArrivedAtRoom': '📦 Your laundry has been delivered',
      'Completed': '✅ Request completed',
      'Declined': '❌ Request declined',
      'Cancelled': '🚫 Request cancelled',
    };

    const title = statusMessages[status] || `Status Update: ${status}`;

    await Notifications.scheduleNotificationAsync({
      content: {
        title,
        body: details || 'Your laundry request status has been updated.',
        data: { type: 'status-change', status },
      },
      trigger: Platform.OS === 'android' ? { channelId: 'status' } : null,
    });
  }

  async testNotification(type: 'robot' | 'message'): Promise<void> {
    if (type === 'robot') {
      await this.sendRobotArrivalNotification('Your Room', true);
    } else {
      await this.sendMessageNotification('Support Team', 'This is a test message notification!');
    }
  }
}

export const notificationService = new NotificationService();
