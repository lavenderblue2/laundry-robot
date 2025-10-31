import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  ActivityIndicator,
  TouchableOpacity,
} from 'react-native';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { laundryService, LaundryRequestResponse } from '../services/laundryService';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedView } from '../components/ThemedView';
import { ThemedText } from '../components/ThemedText';
import { useCustomAlert } from '../components/CustomAlert';
import { ArrowLeft, FileText } from 'lucide-react-native';

export default function ReceiptDetailsScreen() {
  const { requestId } = useLocalSearchParams<{ requestId: string }>();
  const router = useRouter();
  const [request, setRequest] = useState<LaundryRequestResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { showAlert, AlertComponent } = useCustomAlert();

  const backgroundColor = useThemeColor({}, 'background');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const mutedColor = useThemeColor({}, 'muted');

  useEffect(() => {
    loadReceiptData();
  }, [requestId]);

  const loadReceiptData = async () => {
    try {
      setIsLoading(true);
      const requestData = await laundryService.getRequestStatus(Number(requestId));
      setRequest(requestData);
    } catch (error: any) {
      console.error('Error loading receipt:', error);
      showAlert('Error', 'Failed to load receipt');
    } finally {
      setIsLoading(false);
    }
  };

  const formatDate = (dateString?: string) => {
    if (!dateString) return 'N/A';
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      year: 'numeric',
      month: 'long',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatCurrency = (amount: number) => {
    return `₱${amount.toFixed(2)}`;
  };

  if (isLoading) {
    return (
      <ThemedView style={styles.container}>
        <View style={styles.loadingContainer}>
          <ActivityIndicator size="large" color={primaryColor} />
          <ThemedText style={styles.loadingText}>Loading receipt...</ThemedText>
        </View>
        <AlertComponent />
      </ThemedView>
    );
  }

  if (!request || !request.isPaid) {
    return (
      <ThemedView style={styles.container}>
        <View style={styles.errorContainer}>
          <FileText size={64} color={mutedColor} />
          <ThemedText style={[styles.errorText, { color: mutedColor }]}>
            Receipt not available
          </ThemedText>
          <ThemedText style={[styles.errorSubtext, { color: mutedColor }]}>
            {!request ? 'Request not found' : 'This request has not been paid yet'}
          </ThemedText>
          <TouchableOpacity
            style={[styles.backButton, { backgroundColor: primaryColor }]}
            onPress={() => router.back()}
          >
            <ThemedText style={styles.backButtonText}>Go Back</ThemedText>
          </TouchableOpacity>
        </View>
        <AlertComponent />
      </ThemedView>
    );
  }

  // Generate receipt number from request ID
  const receiptNumber = `RCP-${new Date().getFullYear()}-${String(request.id).padStart(6, '0')}`;

  return (
    <View style={[styles.container, { backgroundColor: '#ffffff' }]}>
      <View style={[styles.header, { backgroundColor: '#ffffff', borderBottomColor: '#e5e7eb' }]}>
        <TouchableOpacity
          style={styles.headerBackButton}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} color="#000000" />
        </TouchableOpacity>
        <Text style={[styles.headerTitle, { color: '#000000' }]}>Receipt</Text>
        <View style={styles.headerSpacer} />
      </View>

      <ScrollView style={styles.scrollView} contentContainerStyle={styles.scrollContent}>
        {/* Receipt Card - White background with clean design */}
        <View style={[styles.receiptCard, { backgroundColor: '#ffffff', borderColor: '#d1d5db' }]}>
          {/* Receipt Header */}
          <View style={styles.receiptHeader}>
            <FileText size={48} color={secondaryColor} />
            <Text style={[styles.receiptNumber, { color: '#000000' }]}>{receiptNumber}</Text>
            <Text style={[styles.generatedAt, { color: '#6b7280' }]}>
              Issued: {formatDate(request.completedAt || request.createdAt)}
            </Text>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: '#e5e7eb' }]} />

          {/* Company/Service Info */}
          <View style={styles.section}>
            <Text style={[styles.companyName, { color: '#000000' }]}>LAUNDRY SERVICE</Text>
            <Text style={[styles.companySubtext, { color: '#6b7280' }]}>Official Payment Receipt</Text>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: '#e5e7eb' }]} />

          {/* Customer Info */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: '#6b7280' }]}>CUSTOMER INFORMATION</Text>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: '#000000' }]}>Name:</Text>
              <Text style={[styles.infoValue, { color: '#000000' }]}>{request.customerName || 'N/A'}</Text>
            </View>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: '#000000' }]}>Request #:</Text>
              <Text style={[styles.infoValue, { color: '#000000' }]}>{request.id}</Text>
            </View>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: '#e5e7eb' }]} />

          {/* Service Details */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: '#6b7280' }]}>SERVICE DETAILS</Text>
            {request.weight && (
              <View style={styles.infoRow}>
                <Text style={[styles.infoLabel, { color: '#000000' }]}>Weight:</Text>
                <Text style={[styles.infoValue, { color: '#000000' }]}>{request.weight} kg</Text>
              </View>
            )}
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: '#000000' }]}>Rate per kg:</Text>
              <Text style={[styles.infoValue, { color: '#000000' }]}>{formatCurrency(request.totalCost && request.weight ? request.totalCost / request.weight : 0)}</Text>
            </View>
            {request.scheduledAt && (
              <View style={styles.infoRow}>
                <Text style={[styles.infoLabel, { color: '#000000' }]}>Scheduled:</Text>
                <Text style={[styles.infoValue, { color: '#000000' }]}>{formatDate(request.scheduledAt)}</Text>
              </View>
            )}
            {request.completedAt && (
              <View style={styles.infoRow}>
                <Text style={[styles.infoLabel, { color: '#000000' }]}>Completed:</Text>
                <Text style={[styles.infoValue, { color: '#000000' }]}>{formatDate(request.completedAt)}</Text>
              </View>
            )}
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: '#e5e7eb' }]} />

          {/* Payment Details */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: '#6b7280' }]}>PAYMENT DETAILS</Text>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: '#000000' }]}>Payment Status:</Text>
              <Text style={[styles.infoValue, { color: '#000000' }]}>PAID</Text>
            </View>
            <View style={styles.infoRow}>
              <Text style={[styles.infoLabel, { color: '#000000' }]}>Paid At:</Text>
              <Text style={[styles.infoValue, { color: '#000000' }]}>{formatDate(request.completedAt)}</Text>
            </View>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: '#e5e7eb' }]} />

          {/* Total Amount */}
          <View style={styles.totalSection}>
            <Text style={[styles.totalLabel, { color: '#6b7280' }]}>TOTAL PAID</Text>
            <Text style={[styles.totalAmount, { color: secondaryColor }]}>
              {formatCurrency(request.totalCost || 0)}
            </Text>
          </View>
        </View>

        {/* Footer Note */}
        <View style={styles.footerNote}>
          <Text style={[styles.footerText, { color: '#6b7280' }]}>
            Thank you for your business!
          </Text>
          <Text style={[styles.footerText, { color: '#6b7280' }]}>
            This is an official receipt for your laundry service.
          </Text>
        </View>
      </ScrollView>

      <AlertComponent />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  loadingContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  loadingText: {
    marginTop: 16,
    fontSize: 16,
  },
  errorContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    padding: 24,
  },
  errorText: {
    fontSize: 18,
    marginTop: 16,
    fontWeight: '600',
  },
  errorSubtext: {
    fontSize: 14,
    marginTop: 8,
    marginBottom: 24,
  },
  backButton: {
    paddingHorizontal: 24,
    paddingVertical: 12,
    borderRadius: 8,
  },
  backButtonText: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '600',
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: 16,
    paddingTop: 48,
    paddingBottom: 12,
    borderBottomWidth: 1,
  },
  headerBackButton: {
    padding: 8,
  },
  headerTitle: {
    fontSize: 20,
    fontWeight: '600',
  },
  headerSpacer: {
    width: 40,
  },
  scrollView: {
    flex: 1,
    backgroundColor: '#f9fafb',
  },
  scrollContent: {
    padding: 16,
  },
  receiptCard: {
    borderRadius: 12,
    padding: 24,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  receiptHeader: {
    alignItems: 'center',
    marginBottom: 24,
  },
  receiptNumber: {
    fontSize: 24,
    fontWeight: '700',
    marginTop: 12,
  },
  generatedAt: {
    fontSize: 14,
    marginTop: 4,
  },
  companyName: {
    fontSize: 18,
    fontWeight: '700',
    textAlign: 'center',
    marginBottom: 4,
  },
  companySubtext: {
    fontSize: 12,
    textAlign: 'center',
  },
  divider: {
    height: 1,
    marginVertical: 20,
  },
  section: {
    marginBottom: 4,
  },
  sectionTitle: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 0.5,
    marginBottom: 12,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: 8,
  },
  infoLabel: {
    fontSize: 14,
    flex: 1,
  },
  infoValue: {
    fontSize: 14,
    fontWeight: '500',
    flex: 1,
    textAlign: 'right',
  },
  totalSection: {
    alignItems: 'center',
    paddingTop: 4,
  },
  totalLabel: {
    fontSize: 12,
    fontWeight: '700',
    letterSpacing: 0.5,
    marginBottom: 8,
  },
  totalAmount: {
    fontSize: 36,
    fontWeight: '700',
  },
  footerNote: {
    alignItems: 'center',
    marginTop: 24,
    marginBottom: 40,
  },
  footerText: {
    fontSize: 12,
    textAlign: 'center',
    marginTop: 4,
  },
});
