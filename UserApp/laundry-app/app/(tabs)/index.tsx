import React, { useEffect, useState , useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { laundryService, LaundryRequestResponse } from '../../services/laundryService';
import { useThemeColor } from '../../hooks/useThemeColor';
import { ThemedView } from '../../components/ThemedView';
import { ThemedText } from '../../components/ThemedText';
import { useRouter, useFocusEffect } from 'expo-router';
import { formatRelativeTime } from '../../utils/dateUtils';
import { useCustomAlert } from '../../components/CustomAlert';

export default function HomeScreen() {
  const { user, refreshProfile } = useAuth();
  const router = useRouter();
  const [requests, setRequests] = useState<LaundryRequestResponse[]>([]);
  const [activeRequest, setActiveRequest] = useState<LaundryRequestResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const { showAlert, AlertComponent } = useCustomAlert();
  
  const backgroundColor = useThemeColor({}, 'background');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const cardColor = useThemeColor({}, 'card');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');
  const dangerColor = useThemeColor({}, 'danger');
  const warningColor = useThemeColor({}, 'warning');

  const loadRequests = async () => {
    try {
      setIsLoading(true);
      const [userRequests, active] = await Promise.all([
        laundryService.getUserRequests(),
        laundryService.getActiveRequest()
      ]);
      setRequests(userRequests.sort((a, b) => new Date(b.requestedAt || 0).getTime() - new Date(a.requestedAt || 0).getTime()));
      setActiveRequest(active);
    } catch (error: any) {
      console.error('Error loading requests:', error);
      showAlert('Error', 'Failed to load requests');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadRequests();
  }, []);

  // Refresh data when screen comes into focus
  useFocusEffect(
    useCallback(() => {
      loadRequests();
      refreshProfile();
    }, []) // Remove refreshProfile from dependencies to prevent infinite loop
  );

  const getStatusString = (status: any): string => {
    // Convert backend enum to string
    switch (Number(status)) {
      case 0: return 'pending';
      case 1: return 'accepted';
      case 2: return 'inprogress';
      case 3: return 'robotenroute';
      case 4: return 'arrivedatroom';
      case 5: return 'laundryloaded';
      case 6: return 'returnedtobase';
      case 7: return 'weighingcomplete';
      case 8: return 'paymentpending';
      case 9: return 'completed';
      case 10: return 'declined';
      case 11: return 'cancelled';
      case 12: return 'washing';
      case 13: return 'finishedwashingarrivedatroom';
      case 14: return 'finishedwashinggoingtoroom';
      case 15: return 'finishedwashing';
      case 27: return 'finishedwashinggoingtobase';
      case 28: return 'finishedwashingawaitingpickup';
      case 29: return 'finishedwashingatbase';
      default: return String(status).toLowerCase();
    }
  };

  const getStatusDisplay = (status: any) => {
    const statusStr = getStatusString(status);
    const statusInfo = {
      'pending': { display: '⏳ Awaiting Approval', description: 'Request submitted, waiting for admin approval', color: warningColor },
      'accepted': { display: '✅ Approved', description: 'Request approved, robot will be dispatched soon', color: primaryColor },
      'inprogress': { display: '🔄 Processing', description: 'Request is being processed', color: primaryColor },
      'robotenroute': { display: '🤖 Robot En Route', description: 'Robot is navigating to your room', color: primaryColor },
      'arrivedatroom': { display: '📍 Robot Arrived', description: 'Robot has arrived at your room for pickup', color: secondaryColor },
      'laundryloaded': { display: '📦 Laundry Loaded', description: 'Laundry loaded, robot returning to base', color: primaryColor },
      'returnedtobase': { display: '🏠 Returned to Base', description: 'Robot has returned with your laundry', color: primaryColor },
      'weighingcomplete': { display: '⚖️ Weighing Complete', description: 'Your laundry has been weighed', color: primaryColor },
      'paymentpending': { display: '💳 Payment Due', description: 'Waiting for payment completion', color: warningColor },
      'completed': { display: '✅ Completed', description: 'Service completed successfully', color: secondaryColor },
      'declined': { display: '❌ Declined', description: 'Request was declined by admin', color: dangerColor },
      'cancelled': { display: '🚫 Cancelled', description: 'Request was cancelled', color: mutedColor },
      'washing': { display: '🌊 Washing', description: 'Your laundry is being washed', color: primaryColor },
      'finishedwashing': { display: '✨ Ready for Pickup', description: 'Laundry is clean and ready for pickup or delivery', color: secondaryColor },
      'finishedwashinggoingtoroom': { display: '🚚 Delivery in Progress', description: 'Robot is delivering your clean laundry', color: primaryColor },
      'finishedwashingarrivedatroom': { display: '📍 Delivery Arrived', description: 'Robot has arrived with your clean laundry', color: secondaryColor },
      'finishedwashinggoingtobase': { display: '🏠 Returning to Base', description: 'Robot is returning after delivery', color: primaryColor },
      'finishedwashingawaitingpickup': { display: '📦 Ready for Pickup', description: 'Clean laundry is ready for pickup', color: secondaryColor },
      'finishedwashingatbase': { display: '✅ Complete - Admin Finalizing', description: 'Service completed, admin is finalizing your request', color: secondaryColor },
    };
    return statusInfo[statusStr] || { display: String(status), description: 'Status update', color: mutedColor };
  };

  const getStatusColor = (status: any) => {
    return getStatusDisplay(status).color;
  };

  return (
    <ThemedView style={styles.container}>
      <ScrollView 
        style={styles.scrollContainer}
        refreshControl={
          <RefreshControl refreshing={isLoading} onRefresh={loadRequests} />
        }
      >
        <View style={styles.header}>
          <ThemedText style={[styles.greeting, { color: mutedColor }]}>Welcome back,</ThemedText>
          <ThemedText style={styles.userName}>{user?.customerName || 'User'}</ThemedText>
          {user?.roomName && (
            <ThemedText style={[styles.roomInfo, { color: secondaryColor }]}>
              📍 Room: {user.roomName}
            </ThemedText>
          )}
        </View>

        <View style={styles.section}>
          <ThemedText style={styles.sectionTitle}>Recent Requests</ThemedText>
        {requests.length === 0 ? (
          <View style={styles.emptyState}>
            <ThemedText style={[styles.emptyText, { color: mutedColor }]}>No requests yet</ThemedText>
            <ThemedText style={[styles.emptySubtext, { color: mutedColor }]}>Tap &apos;New Request&apos; below to create your first laundry request</ThemedText>
          </View>
        ) : (
          <>
            {/* Active Request Alert */}
            {activeRequest && (
              <TouchableOpacity
                style={[styles.activeRequestCard, { backgroundColor: primaryColor + '20', borderColor: primaryColor }]}
                onPress={() => router.push('/active-request')}
              >
                <View style={styles.activeRequestContent}>
                  <ThemedText style={[styles.activeRequestTitle, { color: primaryColor }]}>
                    🤖 Active Request #{activeRequest.id}
                  </ThemedText>
                  <ThemedText style={[styles.activeRequestStatus, { color: textColor }]}>
                    {getStatusDisplay(activeRequest.status).display}
                  </ThemedText>
                  <ThemedText style={[styles.activeRequestDescription, { color: mutedColor }]}>
                    {getStatusDisplay(activeRequest.status).description}
                  </ThemedText>
                  {activeRequest.assignedRobot && (
                    <ThemedText style={[styles.activeRequestRobot, { color: mutedColor }]}>
                      Robot: {activeRequest.assignedRobot}
                    </ThemedText>
                  )}
                </View>
                <ThemedText style={[styles.activeRequestArrow, { color: primaryColor }]}>›</ThemedText>
              </TouchableOpacity>
            )}
            
            {/* Recent Requests */}
            {requests.slice(0, 3).map((request) => (
              <View key={request.id} style={[styles.requestCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                <View style={styles.requestHeader}>
                  <ThemedText style={styles.requestId}>Request #{request.id}</ThemedText>
                  <View style={[styles.statusBadge, { backgroundColor: getStatusColor(request.status) }]}>
                    <Text style={styles.statusText}>{getStatusDisplay(request.status).display}</Text>
                  </View>
                </View>
                <ThemedText style={[styles.requestDate, { color: mutedColor }]}>
                  {formatRelativeTime(request.requestedAt)}
                </ThemedText>
                {request.weight && (
                  <ThemedText style={[styles.requestWeight, { color: mutedColor }]}>Weight: {request.weight}kg</ThemedText>
                )}
                {request.totalCost && (
                  <ThemedText style={[styles.requestCost, { color: secondaryColor }]}>Cost: ₱{request.totalCost}</ThemedText>
                )}
              </View>
            ))}
          </>
        )}
      </View>

        <View style={styles.quickActions}>
          <ThemedText style={styles.sectionTitle}>Quick Actions</ThemedText>
          {activeRequest ? (
            <TouchableOpacity 
              style={[styles.actionButton, { backgroundColor: mutedColor }]}
              disabled={true}
            >
              <Text style={styles.actionButtonText}>Request In Progress</Text>
            </TouchableOpacity>
          ) : (
            <TouchableOpacity 
              style={[styles.actionButton, { backgroundColor: primaryColor }]}
              onPress={() => router.push('/(tabs)/request')}
            >
              <Text style={styles.actionButtonText}>New Request</Text>
            </TouchableOpacity>
          )}
          <TouchableOpacity 
            style={[styles.actionButtonSecondary, { backgroundColor: cardColor, borderColor: borderColor }]}
            onPress={() => router.push('/(tabs)/history')}
          >
            <ThemedText style={[styles.actionButtonSecondaryText, { color: textColor }]}>View History</ThemedText>
          </TouchableOpacity>
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
    padding: 24,
    paddingTop: 60,
  },
  greeting: {
    fontSize: 18,
  },
  userName: {
    fontSize: 28,
    fontWeight: 'bold',
    marginTop: 4,
  },
  roomInfo: {
    fontSize: 16,
    fontWeight: '500',
    marginTop: 8,
  },
  section: {
    padding: 24,
    paddingTop: 0,
  },
  sectionTitle: {
    fontSize: 20,
    fontWeight: '600',
    marginBottom: 16,
  },
  emptyState: {
    alignItems: 'center',
    padding: 32,
  },
  emptyText: {
    fontSize: 16,
    fontWeight: '500',
  },
  emptySubtext: {
    fontSize: 14,
    textAlign: 'center',
    marginTop: 8,
  },
  requestCard: {
    borderRadius: 12,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.1,
    shadowRadius: 3,
    elevation: 2,
  },
  requestHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  requestId: {
    fontSize: 16,
    fontWeight: '600',
  },
  statusBadge: {
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '500',
  },
  requestDate: {
    fontSize: 14,
    marginBottom: 4,
  },
  requestWeight: {
    fontSize: 14,
  },
  requestCost: {
    fontSize: 14,
    fontWeight: '500',
  },
  quickActions: {
    padding: 24,
    paddingTop: 0,
  },
  actionButton: {
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    marginBottom: 12,
  },
  actionButtonText: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '600',
  },
  actionButtonSecondary: {
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    borderWidth: 1,
  },
  actionButtonSecondaryText: {
    fontSize: 16,
    fontWeight: '600',
  },
  activeRequestCard: {
    flexDirection: 'row',
    alignItems: 'center',
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    borderWidth: 2,
  },
  activeRequestContent: {
    flex: 1,
  },
  activeRequestTitle: {
    fontSize: 16,
    fontWeight: '600',
    marginBottom: 4,
  },
  activeRequestStatus: {
    fontSize: 14,
    fontWeight: '500',
  },
  activeRequestDescription: {
    fontSize: 12,
    marginTop: 2,
  },
  activeRequestRobot: {
    fontSize: 12,
    marginTop: 4,
  },
  activeRequestArrow: {
    fontSize: 18,
    fontWeight: 'bold',
  },
});
