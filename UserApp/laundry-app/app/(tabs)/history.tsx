import React, { useEffect, useState , useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  RefreshControl,
} from 'react-native';
import { laundryService, LaundryRequestResponse } from '../../services/laundryService';
import { useThemeColor } from '../../hooks/useThemeColor';
import { ThemedView } from '../../components/ThemedView';
import { ThemedText } from '../../components/ThemedText';
import { useRouter, useFocusEffect } from 'expo-router';
import { formatRelativeTime } from '../../utils/dateUtils';

import { useCustomAlert } from '../../components/CustomAlert';

export default function HistoryScreen() {
  const router = useRouter();
  const [requests, setRequests] = useState<LaundryRequestResponse[]>([]);
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
      const userRequests = await laundryService.getUserRequests();
      setRequests(userRequests.sort((a, b) => new Date(b.requestedAt || 0).getTime() - new Date(a.requestedAt || 0).getTime()));
    } catch (error: any) {
      showAlert('Error', 'Failed to load request history');
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
    }, [])
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
      case 'completed': return secondaryColor;
      case 'declined': return dangerColor;
      case 'cancelled': return mutedColor;
      default: return mutedColor;
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

  const handleViewRequest = (requestId: number) => {
    router.push(`/request-details?requestId=${requestId}`);
  };

  const renderRequestCard = ({ item: request }: { item: LaundryRequestResponse }) => (
    <View style={[styles.requestCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
      <View style={styles.requestHeader}>
        <View>
          <ThemedText style={styles.requestId}>Request #{request.id}</ThemedText>
          <ThemedText style={[styles.requestType, { color: mutedColor }]}>
            {getRequestTypeLabel(request.type || 2)}
          </ThemedText>
        </View>
        <View style={[styles.statusBadge, { backgroundColor: getStatusColor(request.status) }]}>
          <Text style={styles.statusText}>{request.status}</Text>
        </View>
      </View>

      <View style={styles.requestDetails}>
        <View style={styles.detailRow}>
          <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Requested:</ThemedText>
          <ThemedText style={[styles.detailValue, { color: textColor }]}>
            {formatRelativeTime(request.requestedAt)}
          </ThemedText>
        </View>
        <View style={styles.detailRow}>
          <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Scheduled:</ThemedText>
          <ThemedText style={[styles.detailValue, { color: textColor }]}>
            {formatRelativeTime(request.scheduledAt)}
          </ThemedText>
        </View>
        {request.weight && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Weight:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>{request.weight}kg</ThemedText>
          </View>
        )}
        {request.totalCost && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Cost:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: secondaryColor }]}>₱{request.totalCost}</ThemedText>
          </View>
        )}
        {request.assignedRobot && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Robot:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>{request.assignedRobot}</ThemedText>
          </View>
        )}
        {request.completedAt && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Completed:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>
              {formatRelativeTime(request.completedAt)}
            </ThemedText>
          </View>
        )}
        {request.declineReason && (
          <View style={[styles.declineReason, { backgroundColor: dangerColor + '20', borderLeftColor: dangerColor }]}>
            <ThemedText style={[styles.declineLabel, { color: dangerColor }]}>Decline Reason:</ThemedText>
            <ThemedText style={[styles.declineText, { color: dangerColor }]}>{request.declineReason}</ThemedText>
          </View>
        )}
      </View>

      <TouchableOpacity
        style={[styles.trackButton, { backgroundColor: cardColor, borderColor: borderColor }]}
        onPress={() => handleViewRequest(request.id)}
      >
        <ThemedText style={[styles.trackButtonText, { color: primaryColor }]}>View Details</ThemedText>
      </TouchableOpacity>
    </View>
  );

  const renderHeader = () => (
    <View style={styles.header}>
      <ThemedText style={styles.title}>Request History</ThemedText>
      <ThemedText style={[styles.subtitle, { color: mutedColor }]}>Track your laundry requests</ThemedText>
    </View>
  );

  const renderEmpty = () => (
    <View style={styles.emptyState}>
      <ThemedText style={[styles.emptyText, { color: mutedColor }]}>No requests yet</ThemedText>
      <ThemedText style={[styles.emptySubtext, { color: mutedColor }]}>Your laundry requests will appear here</ThemedText>
    </View>
  );

  return (
    <ThemedView style={styles.container}>
      <FlatList
        data={requests}
        renderItem={renderRequestCard}
        keyExtractor={(item) => item.id.toString()}
        ListHeaderComponent={renderHeader}
        ListEmptyComponent={renderEmpty}
        contentContainerStyle={styles.listContent}
        refreshControl={
          <RefreshControl refreshing={isLoading} onRefresh={loadRequests} />
        }
        initialNumToRender={10}
        maxToRenderPerBatch={10}
        windowSize={10}
        removeClippedSubviews={true}
        getItemLayout={(data, index) => ({
          length: 350,
          offset: 350 * index,
          index,
        })}
      />
      <AlertComponent />
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  listContent: {
    flexGrow: 1,
  },
  header: {
    padding: 24,
    paddingTop: 60,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
  },
  emptyState: {
    alignItems: 'center',
    padding: 48,
  },
  emptyText: {
    fontSize: 18,
    fontWeight: '500',
  },
  emptySubtext: {
    fontSize: 14,
    textAlign: 'center',
    marginTop: 8,
  },
  requestCard: {
    marginHorizontal: 16,
    marginVertical: 8,
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  requestHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 16,
  },
  requestId: {
    fontSize: 18,
    fontWeight: '600',
  },
  requestType: {
    fontSize: 14,
    marginTop: 2,
  },
  statusBadge: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '600',
  },
  requestDetails: {
    gap: 8,
    marginBottom: 16,
  },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  detailLabel: {
    fontSize: 14,
  },
  detailValue: {
    fontSize: 14,
    fontWeight: '500',
  },
  declineReason: {
    marginTop: 8,
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
  },
  declineLabel: {
    fontSize: 12,
    fontWeight: '600',
    marginBottom: 4,
  },
  declineText: {
    fontSize: 14,
  },
  trackButton: {
    borderRadius: 8,
    padding: 12,
    alignItems: 'center',
    borderWidth: 1,
  },
  trackButtonText: {
    fontSize: 14,
    fontWeight: '600',
  },
});