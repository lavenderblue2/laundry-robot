import React, { useEffect, useState , useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { useRouter, useLocalSearchParams, Stack, useFocusEffect } from 'expo-router';

import { laundryService, LaundryRequestResponse } from '../services/laundryService';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedView } from '../components/ThemedView';
import { ThemedText } from '../components/ThemedText';
import { Package, MapPin, Clock, CheckCircle, Truck, ArrowLeft, User, Calendar, DollarSign, FileText } from 'lucide-react-native';
import { formatRelativeTime } from '../utils/dateUtils';
import { useCustomAlert } from '../components/CustomAlert';

export default function RequestDetailsScreen() {
  const router = useRouter();
  const { requestId } = useLocalSearchParams();
  const [request, setRequest] = useState<LaundryRequestResponse | null>(null);
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

  const loadRequestDetails = async () => {
    if (!requestId) return;
    
    try {
      setIsLoading(true);
      const requestData = await laundryService.getRequestStatus(Number(requestId));
      setRequest(requestData);
    } catch (error: any) {
      console.error('Error loading request details:', error);
      showAlert('Error', 'Failed to load request details');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadRequestDetails();
  }, [requestId]);

  // Refresh data when screen comes into focus
  useFocusEffect(
    useCallback(() => {
      loadRequestDetails();
    }, [requestId])
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
      default: return String(status).toLowerCase();
    }
  };

  const getStatusColor = (status: any) => {
    const statusStr = getStatusString(status);
    switch (statusStr) {
      case 'pending': return warningColor;
      case 'accepted': return primaryColor;
      case 'inprogress': return primaryColor;
      case 'robotenroute': return primaryColor;
      case 'arrivedatroom': return secondaryColor;
      case 'laundryloaded': return secondaryColor;
      case 'returnedtobase': return secondaryColor;
      case 'weighingcomplete': return secondaryColor;
      case 'completed': return secondaryColor;
      case 'declined': return dangerColor;
      case 'cancelled': return mutedColor;
      default: return mutedColor;
    }
  };

  const getStatusInfo = (status: any) => {
    const statusStr = getStatusString(status);
    switch (statusStr) {
      case 'pending':
        return { 
          icon: Clock, 
          title: 'Request Pending', 
          description: 'Waiting for admin approval' 
        };
      case 'accepted':
        return { 
          icon: CheckCircle, 
          title: 'Request Accepted', 
          description: 'Robot will be dispatched soon' 
        };
      case 'inprogress':
      case 'robotenroute':
        return { 
          icon: Truck, 
          title: 'Robot En Route', 
          description: 'Robot is navigating to your room' 
        };
      case 'arrivedatroom':
        return { 
          icon: MapPin, 
          title: 'Robot Arrived', 
          description: 'Robot has arrived at your room' 
        };
      case 'laundryloaded':
        return { 
          icon: Package, 
          title: 'Laundry Loaded', 
          description: 'Robot is returning to base' 
        };
      case 'returnedtobase':
        return { 
          icon: CheckCircle, 
          title: 'Returned to Base', 
          description: 'Robot has returned with your laundry' 
        };
      case 'weighingcomplete':
        return { 
          icon: CheckCircle, 
          title: 'Weighing Complete', 
          description: 'Your laundry has been weighed' 
        };
      case 'completed':
        return { 
          icon: CheckCircle, 
          title: 'Completed', 
          description: 'Your laundry service is complete' 
        };
      case 'declined':
        return { 
          icon: Clock, 
          title: 'Request Declined', 
          description: 'Request was declined by admin' 
        };
      case 'cancelled':
        return { 
          icon: Clock, 
          title: 'Request Cancelled', 
          description: 'Request was cancelled' 
        };
      default:
        return { 
          icon: Clock, 
          title: status, 
          description: 'Status update' 
        };
    }
  };

  const getRequestTypeLabel = (type: number) => {
    switch (type) {
      case 0: return 'Pickup Only';
      case 1: return 'Delivery Only';
      case 2: return 'Pickup & Delivery';
      default: return 'Unknown';
    }
  };

  if (!request) {
    return (
      <>
        <Stack.Screen options={{ headerShown: false }} />
        <ThemedView style={styles.container}>
          <ScrollView 
            style={styles.scrollContainer}
            refreshControl={
              <RefreshControl refreshing={isLoading} onRefresh={loadRequestDetails} />
            }
          >
            <View style={styles.loadingState}>
              <Package size={64} color={mutedColor} />
              <ThemedText style={[styles.loadingText, { color: mutedColor }]}>
                {isLoading ? 'Loading request details...' : 'Request not found'}
              </ThemedText>
            </View>
          </ScrollView>
        </ThemedView>
      </>
    );
  }

  const statusInfo = getStatusInfo(request.status);
  const StatusIcon = statusInfo.icon;
  const statusColor = getStatusColor(request.status);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <ThemedView style={styles.container}>
      <ScrollView 
        style={styles.scrollContainer}
        refreshControl={
          <RefreshControl refreshing={isLoading} onRefresh={loadRequestDetails} />
        }
      >

        {/* Header */}
        <View style={styles.header}>
          <TouchableOpacity
            style={[styles.backButton, { backgroundColor: cardColor, borderColor: borderColor }]}
            onPress={() => router.push('/(tabs)/history')}
          >
            <ArrowLeft size={20} color={textColor} />
          </TouchableOpacity>
          <StatusIcon size={48} color={statusColor} />
          <ThemedText style={styles.title}>Request #{request.id}</ThemedText>
          <ThemedText style={[styles.subtitle, { color: mutedColor }]}>
            {request.customerName}
          </ThemedText>
        </View>

        {/* Status Card */}
        <View style={[styles.statusCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
          <View style={styles.statusHeader}>
            <StatusIcon size={24} color={statusColor} />
            <View style={styles.statusText}>
              <ThemedText style={styles.statusTitle}>{statusInfo.title}</ThemedText>
              <ThemedText style={[styles.statusDescription, { color: mutedColor }]}>
                {statusInfo.description}
              </ThemedText>
            </View>
            <View style={[styles.statusBadge, { backgroundColor: statusColor }]}>
              <Text style={styles.statusBadgeText}>{request.status}</Text>
            </View>
          </View>
        </View>

        {/* Request Details Card */}
        <View style={[styles.detailsCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
          <ThemedText style={styles.cardTitle}>Request Information</ThemedText>
          
          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <Package size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Type:</ThemedText>
              <ThemedText style={styles.detailValue}>{getRequestTypeLabel(request.type || 2)}</ThemedText>
            </View>
          </View>

          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <Calendar size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Requested:</ThemedText>
              <ThemedText style={styles.detailValue}>
                {formatRelativeTime(request.requestedAt)}
              </ThemedText>
            </View>
          </View>

          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <Clock size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Scheduled:</ThemedText>
              <ThemedText style={styles.detailValue}>
                {formatRelativeTime(request.scheduledAt)}
              </ThemedText>
            </View>
          </View>

          {request.assignedRobot && (
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <Truck size={16} color={mutedColor} />
              </View>
              <View style={styles.detailContent}>
                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Assigned Robot:</ThemedText>
                <ThemedText style={styles.detailValue}>{request.assignedRobot}</ThemedText>
              </View>
            </View>
          )}

          {request.weight && (
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <Package size={16} color={mutedColor} />
              </View>
              <View style={styles.detailContent}>
                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Weight:</ThemedText>
                <ThemedText style={styles.detailValue}>{request.weight}kg</ThemedText>
              </View>
            </View>
          )}

          {request.totalCost && (
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <DollarSign size={16} color={secondaryColor} />
              </View>
              <View style={styles.detailContent}>
                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Total Cost:</ThemedText>
                <ThemedText style={[styles.detailValue, { color: secondaryColor }]}>
                  ₱{request.totalCost}
                </ThemedText>
              </View>
            </View>
          )}

          {request.isPaid && (
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <CheckCircle size={16} color={secondaryColor} />
              </View>
              <View style={styles.detailContent}>
                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Payment Status:</ThemedText>
                <ThemedText style={[styles.detailValue, { color: secondaryColor, fontWeight: '600' }]}>
                  Paid
                </ThemedText>
              </View>
            </View>
          )}

          {request.completedAt && (
            <View style={styles.detailRow}>
              <View style={styles.detailIcon}>
                <CheckCircle size={16} color={secondaryColor} />
              </View>
              <View style={styles.detailContent}>
                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Completed:</ThemedText>
                <ThemedText style={styles.detailValue}>
                  {formatRelativeTime(request.completedAt)}
                </ThemedText>
              </View>
            </View>
          )}
        </View>

        {/* Customer Information Card */}
        <View style={[styles.detailsCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
          <ThemedText style={styles.cardTitle}>Customer Information</ThemedText>
          
          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <User size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Name:</ThemedText>
              <ThemedText style={styles.detailValue}>{request.customerName || 'N/A'}</ThemedText>
            </View>
          </View>

          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <User size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>ID:</ThemedText>
              <ThemedText style={styles.detailValue}>{request.customerId || 'N/A'}</ThemedText>
            </View>
          </View>

          <View style={styles.detailRow}>
            <View style={styles.detailIcon}>
              <MapPin size={16} color={mutedColor} />
            </View>
            <View style={styles.detailContent}>
              <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Address:</ThemedText>
              <ThemedText style={styles.detailValue}>{request.address || 'N/A'}</ThemedText>
            </View>
          </View>
        </View>

        {/* Decline Reason Card */}
        {request.declineReason && (
          <View style={[styles.declineCard, { backgroundColor: dangerColor + '20', borderColor: dangerColor }]}>
            <ThemedText style={[styles.cardTitle, { color: dangerColor }]}>Decline Reason</ThemedText>
            <ThemedText style={[styles.declineText, { color: dangerColor }]}>
              {request.declineReason}
            </ThemedText>
          </View>
        )}

        {/* Instructions Card */}
        {request.instructions && (
          <View style={[styles.detailsCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <ThemedText style={styles.cardTitle}>Special Instructions</ThemedText>
            <ThemedText style={[styles.instructionsText, { color: textColor }]}>
              {request.instructions}
            </ThemedText>
          </View>
        )}

        {/* View Receipt Button */}
        {request.isPaid && (
          <TouchableOpacity
            style={[styles.receiptButton, { backgroundColor: secondaryColor }]}
            onPress={() => router.push(`/receipt?requestId=${request.id}`)}
          >
            <FileText size={20} color="#ffffff" />
            <Text style={styles.receiptButtonText}>View Receipt</Text>
          </TouchableOpacity>
        )}
      </ScrollView>
      <AlertComponent />
    </ThemedView>
    </>
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
    position: 'relative',
  },
  backButton: {
    position: 'absolute',
    top: 50,
    left: 20,
    width: 40,
    height: 40,
    borderRadius: 20,
    alignItems: 'center',
    justifyContent: 'center',
    borderWidth: 1,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginTop: 16,
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
  },
  statusCard: {
    margin: 16,
    padding: 20,
    borderRadius: 16,
    borderWidth: 1,
  },
  statusHeader: {
    flexDirection: 'row',
    alignItems: 'center',
  },
  statusText: {
    flex: 1,
    marginLeft: 16,
  },
  statusTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 4,
  },
  statusDescription: {
    fontSize: 14,
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusBadgeText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '600',
  },
  detailsCard: {
    margin: 16,
    marginTop: 0,
    padding: 20,
    borderRadius: 16,
    borderWidth: 1,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 16,
  },
  detailRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    marginBottom: 16,
  },
  detailIcon: {
    width: 24,
    alignItems: 'center',
    marginRight: 12,
    marginTop: 2,
  },
  detailContent: {
    flex: 1,
  },
  detailLabel: {
    fontSize: 14,
    marginBottom: 4,
  },
  detailValue: {
    fontSize: 16,
    fontWeight: '500',
  },
  declineCard: {
    margin: 16,
    marginTop: 0,
    padding: 20,
    borderRadius: 16,
    borderWidth: 2,
  },
  declineText: {
    fontSize: 14,
    lineHeight: 20,
  },
  instructionsText: {
    fontSize: 14,
    lineHeight: 20,
  },
  loadingState: {
    alignItems: 'center',
    justifyContent: 'center',
    padding: 48,
    marginTop: 100,
  },
  loadingText: {
    fontSize: 16,
    marginTop: 16,
    textAlign: 'center',
  },
  receiptButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    margin: 16,
    marginTop: 8,
    padding: 16,
    borderRadius: 12,
  },
  receiptButtonText: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '600',
    marginLeft: 8,
  },
});