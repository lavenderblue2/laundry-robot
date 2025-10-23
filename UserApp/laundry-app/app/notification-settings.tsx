import { router } from 'expo-router';
import React, { useEffect, useState } from 'react';
import {
  ScrollView,
  StyleSheet,
  Switch,
  Text,
  TouchableOpacity,
  View,
  Alert,
} from 'react-native';
import { useCustomAlert } from '../components/CustomAlert';
import { ThemedText } from '../components/ThemedText';
import { ThemedView } from '../components/ThemedView';
import { useThemeColor } from '../hooks/useThemeColor';
import { notificationService, NotificationSettings } from '../services/notificationService';

export default function NotificationSettingsScreen() {
  const { showAlert, AlertComponent } = useCustomAlert();
  const [settings, setSettings] = useState<NotificationSettings>({
    notificationsEnabled: true,
    vibrationEnabled: true,
    robotArrivalEnabled: true,
    robotDeliveryEnabled: true,
    messagesEnabled: true,
    statusChangesEnabled: true,
    robotArrivalSound: 'default',
    messageSound: 'default',
  });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  const backgroundColor = useThemeColor({}, 'background');
  const cardColor = useThemeColor({}, 'card');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const mutedColor = useThemeColor({}, 'textMuted');
  const borderColor = useThemeColor({}, 'border');
  const successColor = useThemeColor({}, 'success');

  useEffect(() => {
    loadSettings();
  }, []);

  const loadSettings = async () => {
    try {
      const loaded = await notificationService.loadSettings();
      setSettings(loaded);
    } catch (error) {
      showAlert('Error', 'Failed to load notification settings', 'error');
    } finally {
      setIsLoading(false);
    }
  };

  const handleSave = async () => {
    try {
      setIsSaving(true);
      await notificationService.saveSettings(settings);
      showAlert('Success', 'Notification settings saved successfully!', 'success');
    } catch (error) {
      showAlert('Error', 'Failed to save settings', 'error');
    } finally {
      setIsSaving(false);
    }
  };

  const requestPermission = async () => {
    const granted = await notificationService.requestPermissions();
    if (granted) {
      showAlert('Success', 'Notification permission granted!', 'success');
      setSettings({ ...settings, notificationsEnabled: true });
    } else {
      showAlert(
        'Permission Denied',
        'You need to enable notifications in your device settings.',
        'error'
      );
    }
  };

  const testNotification = async (type: 'robot' | 'message') => {
    if (!settings.notificationsEnabled) {
      showAlert('Disabled', 'Please enable notifications first', 'warning');
      return;
    }

    await notificationService.testNotification(type);
    showAlert('Test Sent', `${type === 'robot' ? 'Robot arrival' : 'Message'} test notification sent!`, 'success');
  };

  const updateSetting = <K extends keyof NotificationSettings>(
    key: K,
    value: NotificationSettings[K]
  ) => {
    setSettings({ ...settings, [key]: value });
  };

  if (isLoading) {
    return (
      <ThemedView style={[styles.container, { backgroundColor }]}>
        <View style={[styles.header, { borderBottomColor: borderColor }]}>
          <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
            <Text style={[styles.backButtonText, { color: primaryColor }]}>←</Text>
          </TouchableOpacity>
          <ThemedText style={styles.headerTitle}>Notification Settings</ThemedText>
          <View style={styles.headerRight} />
        </View>
        <View style={styles.loadingContainer}>
          <ThemedText style={{ color: mutedColor }}>Loading settings...</ThemedText>
        </View>
        <AlertComponent />
      </ThemedView>
    );
  }

  return (
    <ThemedView style={[styles.container, { backgroundColor }]}>
      <View style={[styles.header, { borderBottomColor: borderColor }]}>
        <TouchableOpacity onPress={() => router.back()} style={styles.backButton}>
          <Text style={[styles.backButtonText, { color: primaryColor }]}>←</Text>
        </TouchableOpacity>
        <ThemedText style={styles.headerTitle}>Notification Settings</ThemedText>
        <View style={styles.headerRight} />
      </View>

      <ScrollView style={styles.scrollView} contentContainerStyle={styles.contentContainer}>
        {/* Master Controls */}
        <View style={[styles.section, { backgroundColor: cardColor }]}>
          <ThemedText style={styles.sectionTitle}>General</ThemedText>

          <View style={[styles.settingRow, { borderBottomColor: borderColor }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                Enable Notifications
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                Master switch for all notifications
              </ThemedText>
            </View>
            <Switch
              value={settings.notificationsEnabled}
              onValueChange={(value) => {
                if (value) {
                  requestPermission();
                } else {
                  updateSetting('notificationsEnabled', value);
                }
              }}
              trackColor={{ false: borderColor, true: primaryColor }}
            />
          </View>

          <View style={[styles.settingRow, { borderBottomWidth: 0 }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                Vibration
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                Vibrate when receiving notifications
              </ThemedText>
            </View>
            <Switch
              value={settings.vibrationEnabled}
              onValueChange={(value) => updateSetting('vibrationEnabled', value)}
              trackColor={{ false: borderColor, true: primaryColor }}
              disabled={!settings.notificationsEnabled}
            />
          </View>
        </View>

        {/* Notification Events */}
        <View style={[styles.section, { backgroundColor: cardColor }]}>
          <ThemedText style={styles.sectionTitle}>Notification Events</ThemedText>
          <ThemedText style={[styles.sectionDescription, { color: mutedColor }]}>
            Choose which events trigger notifications
          </ThemedText>

          <View style={[styles.settingRow, { borderBottomColor: borderColor }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                🤖 Robot Arriving (Pickup)
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                When robot arrives to collect laundry
              </ThemedText>
            </View>
            <Switch
              value={settings.robotArrivalEnabled}
              onValueChange={(value) => updateSetting('robotArrivalEnabled', value)}
              trackColor={{ false: borderColor, true: primaryColor }}
              disabled={!settings.notificationsEnabled}
            />
          </View>

          <View style={[styles.settingRow, { borderBottomColor: borderColor }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                📦 Robot Arriving (Delivery)
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                When robot delivers clean laundry
              </ThemedText>
            </View>
            <Switch
              value={settings.robotDeliveryEnabled}
              onValueChange={(value) => updateSetting('robotDeliveryEnabled', value)}
              trackColor={{ false: borderColor, true: primaryColor }}
              disabled={!settings.notificationsEnabled}
            />
          </View>

          <View style={[styles.settingRow, { borderBottomColor: borderColor }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                💬 New Messages
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                When you receive support messages
              </ThemedText>
            </View>
            <Switch
              value={settings.messagesEnabled}
              onValueChange={(value) => updateSetting('messagesEnabled', value)}
              trackColor={{ false: borderColor, true: primaryColor }}
              disabled={!settings.notificationsEnabled}
            />
          </View>

          <View style={[styles.settingRow, { borderBottomWidth: 0 }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                📊 Status Changes
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                When your request status updates
              </ThemedText>
            </View>
            <Switch
              value={settings.statusChangesEnabled}
              onValueChange={(value) => updateSetting('statusChangesEnabled', value)}
              trackColor={{ false: borderColor, true: primaryColor }}
              disabled={!settings.notificationsEnabled}
            />
          </View>
        </View>

        {/* Notification Sounds */}
        <View style={[styles.section, { backgroundColor: cardColor }]}>
          <ThemedText style={styles.sectionTitle}>Notification Sounds</ThemedText>

          <View style={[styles.settingRow, { borderBottomColor: borderColor }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                Robot Arrival Sound
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                {settings.robotArrivalSound === 'default' ? 'Default notification sound' : settings.robotArrivalSound}
              </ThemedText>
            </View>
            <Text style={[styles.soundValue, { color: primaryColor }]}>Default</Text>
          </View>

          <View style={[styles.settingRow, { borderBottomWidth: 0 }]}>
            <View style={styles.settingInfo}>
              <ThemedText style={[styles.settingLabel, { color: textColor }]}>
                Message Sound
              </ThemedText>
              <ThemedText style={[styles.settingDescription, { color: mutedColor }]}>
                {settings.messageSound === 'default' ? 'Default notification sound' : settings.messageSound}
              </ThemedText>
            </View>
            <Text style={[styles.soundValue, { color: primaryColor }]}>Default</Text>
          </View>
        </View>

        {/* Test Notifications */}
        <View style={[styles.section, { backgroundColor: cardColor }]}>
          <ThemedText style={styles.sectionTitle}>Test Notifications</ThemedText>

          <TouchableOpacity
            style={[styles.testButton, { backgroundColor: primaryColor, marginBottom: 12 }]}
            onPress={() => testNotification('robot')}
            disabled={!settings.notificationsEnabled}
          >
            <Text style={styles.testButtonText}>🤖 Test Robot Arrival</Text>
          </TouchableOpacity>

          <TouchableOpacity
            style={[styles.testButton, { backgroundColor: primaryColor }]}
            onPress={() => testNotification('message')}
            disabled={!settings.notificationsEnabled}
          >
            <Text style={styles.testButtonText}>💬 Test Message Notification</Text>
          </TouchableOpacity>
        </View>

        {/* Save Button */}
        <TouchableOpacity
          style={[styles.saveButton, { backgroundColor: isSaving ? mutedColor : successColor }]}
          onPress={handleSave}
          disabled={isSaving}
        >
          <Text style={styles.saveButtonText}>
            {isSaving ? 'Saving...' : 'Save Settings'}
          </Text>
        </TouchableOpacity>
      </ScrollView>

      <AlertComponent />
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingTop: 60,
    paddingBottom: 16,
    borderBottomWidth: 1,
  },
  backButton: {
    padding: 8,
  },
  backButtonText: {
    fontSize: 24,
    fontWeight: 'bold',
  },
  headerTitle: {
    fontSize: 18,
    fontWeight: '600',
  },
  headerRight: {
    width: 40,
  },
  scrollView: {
    flex: 1,
  },
  contentContainer: {
    padding: 16,
    paddingBottom: 40,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  section: {
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
  },
  sectionTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 4,
  },
  sectionDescription: {
    fontSize: 13,
    marginBottom: 12,
  },
  settingRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: 12,
    borderBottomWidth: 1,
  },
  settingInfo: {
    flex: 1,
    marginRight: 12,
  },
  settingLabel: {
    fontSize: 15,
    fontWeight: '500',
    marginBottom: 4,
  },
  settingDescription: {
    fontSize: 13,
  },
  soundValue: {
    fontSize: 14,
    fontWeight: '500',
  },
  testButton: {
    padding: 16,
    borderRadius: 8,
    alignItems: 'center',
  },
  testButtonText: {
    color: '#fff',
    fontSize: 15,
    fontWeight: '600',
  },
  saveButton: {
    padding: 16,
    borderRadius: 12,
    alignItems: 'center',
    marginTop: 8,
  },
  saveButtonText: {
    color: '#fff',
    fontSize: 16,
    fontWeight: '600',
  },
});
