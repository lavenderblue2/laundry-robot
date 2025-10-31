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
import { apiGet } from '../services/api';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedView } from '../components/ThemedView';
import { ThemedText } from '../components/ThemedText';
import { useCustomAlert } from '../components/CustomAlert';
import { ArrowLeft, FileText } from 'lucide-react-native';

interface ReceiptData {
  receiptNumber: string;
  generatedAt: string;
  customerName: string;
  customerId: string;
  amount: number;
  paymentMethod: string;
  paidAt: string;
  transactionId: string;
  paymentReference?: string;
  weight?: number;
  ratePerKg: number;
  requestId: number;
  scheduledAt?: string;
  completedAt?: string;
  notes?: string;
}

export default function ReceiptDetailsScreen() {
  const { requestId } = useLocalSearchParams<{ requestId: string }>();
  const router = useRouter();
  const [receipt, setReceipt] = useState<ReceiptData | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const { showAlert, AlertComponent } = useCustomAlert();

  const backgroundColor = useThemeColor({}, 'background');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const cardColor = useThemeColor({}, 'card');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');

  useEffect(() => {
    loadReceipt();
  }, [requestId]);

  const loadReceipt = async () => {
    try {
      setIsLoading(true);
      const response = await apiGet(`/api/Payment/receipt/${requestId}`);

      if (response && !response.message) {
        setReceipt(response);
      } else {
        showAlert('Error', response?.message || 'Receipt not found');
      }
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

  if (!receipt) {
    return (
      <ThemedView style={styles.container}>
        <View style={styles.errorContainer}>
          <FileText size={64} color={mutedColor} />
          <ThemedText style={[styles.errorText, { color: mutedColor }]}>
            Receipt not found
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

  return (
    <ThemedView style={styles.container}>
      <View style={[styles.header, { backgroundColor: cardColor, borderBottomColor: borderColor }]}>
        <TouchableOpacity
          style={styles.headerBackButton}
          onPress={() => router.back()}
        >
          <ArrowLeft size={24} color={textColor} />
        </TouchableOpacity>
        <ThemedText style={styles.headerTitle}>Receipt</ThemedText>
        <View style={styles.headerSpacer} />
      </View>

      <ScrollView style={styles.scrollView} contentContainerStyle={styles.scrollContent}>
        {/* Receipt Header */}
        <View style={[styles.receiptCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
          <View style={styles.receiptHeader}>
            <FileText size={48} color={secondaryColor} />
            <ThemedText style={styles.receiptNumber}>{receipt.receiptNumber}</ThemedText>
            <ThemedText style={[styles.generatedAt, { color: mutedColor }]}>
              Generated: {formatDate(receipt.generatedAt)}
            </ThemedText>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: borderColor }]} />

          {/* Customer Info */}
          <View style={styles.section}>
            <ThemedText style={[styles.sectionTitle, { color: mutedColor }]}>CUSTOMER INFORMATION</ThemedText>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Name:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.customerName}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Request #:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.requestId}</ThemedText>
            </View>
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: borderColor }]} />

          {/* Service Details */}
          <View style={styles.section}>
            <ThemedText style={[styles.sectionTitle, { color: mutedColor }]}>SERVICE DETAILS</ThemedText>
            {receipt.weight && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Weight:</ThemedText>
                <ThemedText style={styles.infoValue}>{receipt.weight} kg</ThemedText>
              </View>
            )}
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Rate per kg:</ThemedText>
              <ThemedText style={styles.infoValue}>{formatCurrency(receipt.ratePerKg)}</ThemedText>
            </View>
            {receipt.scheduledAt && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Scheduled:</ThemedText>
                <ThemedText style={styles.infoValue}>{formatDate(receipt.scheduledAt)}</ThemedText>
              </View>
            )}
            {receipt.completedAt && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Completed:</ThemedText>
                <ThemedText style={styles.infoValue}>{formatDate(receipt.completedAt)}</ThemedText>
              </View>
            )}
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: borderColor }]} />

          {/* Payment Details */}
          <View style={styles.section}>
            <ThemedText style={[styles.sectionTitle, { color: mutedColor }]}>PAYMENT DETAILS</ThemedText>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Transaction ID:</ThemedText>
              <ThemedText style={[styles.infoValue, styles.monospace]}>{receipt.transactionId}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Payment Method:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.paymentMethod}</ThemedText>
            </View>
            {receipt.paymentReference && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Reference:</ThemedText>
                <ThemedText style={[styles.infoValue, styles.monospace]}>{receipt.paymentReference}</ThemedText>
              </View>
            )}
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Paid At:</ThemedText>
              <ThemedText style={styles.infoValue}>{formatDate(receipt.paidAt)}</ThemedText>
            </View>
            {receipt.notes && (
              <View style={styles.notesContainer}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Notes:</ThemedText>
                <ThemedText style={styles.notesText}>{receipt.notes}</ThemedText>
              </View>
            )}
          </View>

          {/* Divider */}
          <View style={[styles.divider, { backgroundColor: borderColor }]} />

          {/* Total Amount */}
          <View style={styles.totalSection}>
            <ThemedText style={[styles.totalLabel, { color: mutedColor }]}>TOTAL PAID</ThemedText>
            <ThemedText style={[styles.totalAmount, { color: secondaryColor }]}>
              {formatCurrency(receipt.amount)}
            </ThemedText>
          </View>
        </View>

        {/* Footer Note */}
        <View style={styles.footerNote}>
          <ThemedText style={[styles.footerText, { color: mutedColor }]}>
            Thank you for your business!
          </ThemedText>
          <ThemedText style={[styles.footerText, { color: mutedColor }]}>
            This is an official receipt for your laundry service.
          </ThemedText>
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
    paddingTop: 60,
    paddingBottom: 16,
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
  monospace: {
    fontFamily: 'monospace',
    fontSize: 12,
  },
  notesContainer: {
    marginTop: 8,
  },
  notesText: {
    fontSize: 14,
    marginTop: 4,
    lineHeight: 20,
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
