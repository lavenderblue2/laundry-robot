import React, { useState, useEffect } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
} from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { laundryService } from '../../services/laundryService';
import { useThemeColor } from '../../hooks/useThemeColor';
import { ThemedView } from '../../components/ThemedView';
import { ThemedText } from '../../components/ThemedText';
import { Package, Clock, CheckCircle } from 'lucide-react-native';
import { useRouter } from 'expo-router';
import { useCustomAlert } from '../../components/CustomAlert';

export default function RequestScreen() {
  const { user } = useAuth();
  const router = useRouter();
  const [isLoading, setIsLoading] = useState(false);
  const [hasActiveRequest, setHasActiveRequest] = useState(false);
  const { showAlert, AlertComponent } = useCustomAlert();
  
  const backgroundColor = useThemeColor({}, 'background');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const cardColor = useThemeColor({}, 'card');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');

  const checkForActiveRequest = async () => {
    try {
      const activeRequest = await laundryService.getActiveRequest();
      setHasActiveRequest(!!activeRequest);
    } catch (error) {
      console.log('No active request found');
      setHasActiveRequest(false);
    }
  };

  useEffect(() => {
    checkForActiveRequest();
  }, []);

  const handleRequestLaundry = async () => {
    if (!user) {
      showAlert('Error', 'Please log in to submit a request');
      return;
    }

    setIsLoading(true);
    try {
      const result = await laundryService.createRequest();
      showAlert('Success', `Laundry request #${result.id} submitted successfully!`, [
        { text: 'OK', onPress: () => router.replace('/active-request') }
      ]);
    } catch (error: any) {
      console.error('Request creation error:', error);
      const errorMessage = error.response?.data?.message || 'Failed to submit request';
      
      // Check if error is about existing active request
      if (error.response?.status === 400 && errorMessage.includes('already have an active request')) {
        showAlert('Active Request Found', 'You already have an active request in progress.', [
          { text: 'View Request', onPress: () => router.replace('/active-request') },
          { text: 'Cancel', style: 'cancel' }
        ]);
      } else {
        showAlert('Error', errorMessage);
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <ThemedView style={styles.container}>
      <ScrollView style={styles.scrollContainer}>
        <View style={styles.header}>
          <Package size={48} color={primaryColor} />
          <ThemedText style={styles.title}>Laundry Service</ThemedText>
          <ThemedText style={[styles.subtitle, { color: mutedColor }]}>
            Request pickup for your laundry
          </ThemedText>
        </View>

        <View style={styles.content}>
          {hasActiveRequest && (
            <View style={[styles.activeRequestAlert, { backgroundColor: primaryColor + '20', borderColor: primaryColor }]}>
              <View style={styles.alertContent}>
                <ThemedText style={[styles.alertTitle, { color: primaryColor }]}>
                  🤖 You have an active request
                </ThemedText>
                <ThemedText style={[styles.alertText, { color: textColor }]}>
                  You can only have one active request at a time. Check your active request or wait for it to complete.
                </ThemedText>
                <TouchableOpacity
                  style={[styles.viewActiveButton, { backgroundColor: primaryColor }]}
                  onPress={() => router.push('/active-request')}
                >
                  <Text style={styles.viewActiveButtonText}>View Active Request</Text>
                </TouchableOpacity>
              </View>
            </View>
          )}

          <View style={[styles.infoCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <Clock size={24} color={primaryColor} />
            <View style={styles.infoTextContainer}>
              <ThemedText style={styles.infoTitle}>How it works</ThemedText>
              <ThemedText style={[styles.infoText, { color: mutedColor }]}>
                1. Request pickup{'\n'}
                2. Robot arrives at your room{'\n'}
                3. Load your laundry{'\n'}
                4. Confirm when done{'\n'}
                5. Robot returns to base for processing
              </ThemedText>
            </View>
          </View>

          <View style={[styles.userCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <CheckCircle size={24} color={secondaryColor} />
            <View style={styles.userInfo}>
              <ThemedText style={styles.userTitle}>Ready to request</ThemedText>
              <ThemedText style={[styles.userSubtitle, { color: mutedColor }]}>
                {user?.customerName}
              </ThemedText>
              <ThemedText style={[styles.userId, { color: mutedColor }]}>
                ID: {user?.customerId}
              </ThemedText>
            </View>
          </View>

          <TouchableOpacity
            style={[styles.requestButton, { backgroundColor: (isLoading || hasActiveRequest) ? mutedColor : primaryColor }]}
            onPress={handleRequestLaundry}
            disabled={isLoading || hasActiveRequest}
          >
            <Package size={24} color="#ffffff" />
            <Text style={styles.requestButtonText}>
              {hasActiveRequest ? 'Request Already Active' : 
               isLoading ? 'Submitting Request...' : 
               'Request Laundry Pickup'}
            </Text>
          </TouchableOpacity>

          <View style={[styles.noteCard, { backgroundColor: cardColor + '50', borderColor: borderColor }]}>
            <ThemedText style={[styles.noteText, { color: mutedColor }]}>
              💡 Make sure your laundry is ready for pickup. The robot will arrive at your assigned room beacon.
            </ThemedText>
          </View>
        </View>
      </ScrollView>
      <AlertComponent />
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  scrollContainer: {
    flex: 1,
  },
  header: {
    alignItems: 'center',
    padding: 32,
    paddingTop: 80,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginTop: 16,
    marginBottom: 8,
  },
  subtitle: {
    fontSize: 16,
    textAlign: 'center',
  },
  content: {
    padding: 24,
    gap: 20,
  },
  infoCard: {
    flexDirection: 'row',
    padding: 20,
    borderRadius: 16,
    borderWidth: 1,
  },
  infoTextContainer: {
    flex: 1,
    marginLeft: 16,
  },
  infoTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 8,
  },
  infoText: {
    fontSize: 14,
    lineHeight: 20,
  },
  userCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 20,
    borderRadius: 16,
    borderWidth: 1,
  },
  userInfo: {
    flex: 1,
    marginLeft: 16,
  },
  userTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 4,
  },
  userSubtitle: {
    fontSize: 16,
    marginBottom: 2,
  },
  userId: {
    fontSize: 12,
  },
  requestButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    padding: 20,
    borderRadius: 16,
    marginTop: 10,
    gap: 12,
  },
  requestButtonText: {
    color: '#ffffff',
    fontSize: 18,
    fontWeight: '600',
  },
  noteCard: {
    padding: 16,
    borderRadius: 12,
    borderWidth: 1,
    marginTop: 10,
  },
  noteText: {
    fontSize: 14,
    textAlign: 'center',
    lineHeight: 20,
  },
  activeRequestAlert: {
    borderRadius: 16,
    padding: 20,
    marginBottom: 20,
    borderWidth: 2,
  },
  alertContent: {
    alignItems: 'center',
  },
  alertTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 8,
    textAlign: 'center',
  },
  alertText: {
    fontSize: 14,
    textAlign: 'center',
    lineHeight: 20,
    marginBottom: 16,
  },
  viewActiveButton: {
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 12,
  },
  viewActiveButtonText: {
    color: '#ffffff',
    fontSize: 14,
    fontWeight: '600',
  },
});