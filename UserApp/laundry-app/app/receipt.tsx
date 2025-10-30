import React, { useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  ScrollView,
  TouchableOpacity,
  RefreshControl,
  Linking,
} from 'react-native';
import { useRouter, useLocalSearchParams, Stack } from 'expo-router';

import { receiptService, Receipt } from '../services/receiptService';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedView } from '../components/ThemedView';
import { ThemedText } from '../components/ThemedText';
import {
  FileText,
  ArrowLeft,
  Building,
  User,
  Package,
  DollarSign,
  CheckCircle,
  AlertCircle,
  ExternalLink
} from 'lucide-react-native';
import { useCustomAlert } from '../components/CustomAlert';

export default function ReceiptScreen() {
  const router = useRouter();
  const { requestId } = useLocalSearchParams();
  const [receipt, setReceipt] = useState<Receipt | null>(null);
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

  const loadReceipt = async () => {
    if (!requestId) return;

    try {
      setIsLoading(true);
      const receiptData = await receiptService.getReceiptByRequest(Number(requestId));
      if (receiptData) {
        setReceipt(receiptData);
      } else {
        showAlert('Not Found', 'No receipt found for this request');
      }
    } catch (error: any) {
      console.error('Error loading receipt:', error);
      showAlert('Error', error.response?.data?.error || 'Failed to load receipt');
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadReceipt();
  }, [requestId]);

  const handleOpenInBrowser = async () => {
    if (!receipt) return;

    try {
      const url = receiptService.getReceiptWebUrl(receipt.id);
      const supported = await Linking.canOpenURL(url);

      if (supported) {
        await Linking.openURL(url);
      } else {
        showAlert('Error', 'Cannot open receipt in browser');
      }
    } catch (error) {
      console.error('Error opening receipt:', error);
      showAlert('Error', 'Failed to open receipt in browser');
    }
  };

  const formatDate = (dateString: string) => {
    const date = new Date(dateString);
    return date.toLocaleDateString('en-US', {
      month: 'long',
      day: 'numeric',
      year: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
      hour12: true,
    });
  };

  if (!receipt) {
    return (
      <>
        <Stack.Screen options={{ headerShown: false }} />
        <ThemedView style={styles.container}>
          <ScrollView
            style={styles.scrollContainer}
            refreshControl={
              <RefreshControl refreshing={isLoading} onRefresh={loadReceipt} />
            }
          >
            <View style={styles.header}>
              <TouchableOpacity
                style={[styles.backButton, { backgroundColor: cardColor, borderColor: borderColor }]}
                onPress={() => router.back()}
              >
                <ArrowLeft size={20} color={textColor} />
              </TouchableOpacity>
            </View>
            <View style={styles.loadingState}>
              <FileText size={64} color={mutedColor} />
              <ThemedText style={[styles.loadingText, { color: mutedColor }]}>
                {isLoading ? 'Loading receipt...' : 'Receipt not found'}
              </ThemedText>
            </View>
          </ScrollView>
        </ThemedView>
      </>
    );
  }

  const isRefunded = receipt.payment.status === 'Refunded';
  const isFullRefund = receipt.payment.refundAmount && receipt.payment.refundAmount >= receipt.totalAmount;
  const netAmount = receipt.totalAmount - (receipt.payment.refundAmount || 0);

  return (
    <>
      <Stack.Screen options={{ headerShown: false }} />
      <ThemedView style={styles.container}>
        <ScrollView
          style={styles.scrollContainer}
          refreshControl={
            <RefreshControl refreshing={isLoading} onRefresh={loadReceipt} />
          }
        >
          {/* Header */}
          <View style={styles.header}>
            <TouchableOpacity
              style={[styles.backButton, { backgroundColor: cardColor, borderColor: borderColor }]}
              onPress={() => router.back()}
            >
              <ArrowLeft size={20} color={textColor} />
            </TouchableOpacity>
            <FileText size={48} color={secondaryColor} />
            <ThemedText style={styles.title}>Official Receipt</ThemedText>
            <ThemedText style={[styles.receiptNumber, { color: secondaryColor }]}>
              #{receipt.receiptNumber}
            </ThemedText>
          </View>

          {/* Refund Banner */}
          {isRefunded && (
            <View style={[
              styles.refundBanner,
              {
                backgroundColor: isFullRefund ? dangerColor + '20' : warningColor + '20',
                borderColor: isFullRefund ? dangerColor : warningColor
              }
            ]}>
              <View style={styles.refundHeader}>
                <AlertCircle size={24} color={isFullRefund ? dangerColor : warningColor} />
                <ThemedText style={[
                  styles.refundTitle,
                  { color: isFullRefund ? dangerColor : warningColor }
                ]}>
                  {isFullRefund ? 'FULLY REFUNDED' : 'PARTIALLY REFUNDED'}
                </ThemedText>
              </View>
              <View style={styles.refundDetails}>
                <ThemedText style={[styles.refundText, { color: textColor }]}>
                  Refund Amount: ₱{receipt.payment.refundAmount?.toFixed(2)}
                </ThemedText>
                {receipt.payment.refundedAt && (
                  <ThemedText style={[styles.refundText, { color: mutedColor }]}>
                    Date: {formatDate(receipt.payment.refundedAt)}
                  </ThemedText>
                )}
                {receipt.payment.refundReason && (
                  <ThemedText style={[styles.refundText, { color: mutedColor }]}>
                    Reason: {receipt.payment.refundReason}
                  </ThemedText>
                )}
              </View>
            </View>
          )}

          {/* Business Information */}
          <View style={[styles.card, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <View style={styles.cardHeader}>
              <Building size={20} color={primaryColor} />
              <ThemedText style={styles.cardTitle}>Business Information</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Name:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.business.businessName}</ThemedText>
            </View>
            {receipt.business.businessAddress && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Address:</ThemedText>
                <ThemedText style={styles.infoValue}>{receipt.business.businessAddress}</ThemedText>
              </View>
            )}
            {receipt.business.businessPhone && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Phone:</ThemedText>
                <ThemedText style={styles.infoValue}>{receipt.business.businessPhone}</ThemedText>
              </View>
            )}
            {receipt.business.businessEmail && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Email:</ThemedText>
                <ThemedText style={styles.infoValue}>{receipt.business.businessEmail}</ThemedText>
              </View>
            )}
          </View>

          {/* Receipt Information */}
          <View style={[styles.card, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Date Issued:</ThemedText>
              <ThemedText style={styles.infoValue}>{formatDate(receipt.generatedAt)}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Transaction ID:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.payment.transactionId}</ThemedText>
            </View>
            {receipt.payment.paymentReference && (
              <View style={styles.infoRow}>
                <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Reference:</ThemedText>
                <ThemedText style={styles.infoValue}>{receipt.payment.paymentReference}</ThemedText>
              </View>
            )}
          </View>

          {/* Customer Information */}
          <View style={[styles.card, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <View style={styles.cardHeader}>
              <User size={20} color={primaryColor} />
              <ThemedText style={styles.cardTitle}>Customer Information</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Name:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.customer.customerName}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Phone:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.customer.customerPhone}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Address:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.customer.customerAddress}</ThemedText>
            </View>
          </View>

          {/* Service Details */}
          <View style={[styles.card, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <View style={styles.cardHeader}>
              <Package size={20} color={primaryColor} />
              <ThemedText style={styles.cardTitle}>Service Details</ThemedText>
            </View>

            {receipt.lineItems.map((item, index) => (
              <View key={index} style={[styles.lineItem, { borderBottomColor: borderColor }]}>
                <View style={styles.lineItemDescription}>
                  <ThemedText style={styles.lineItemTitle}>{item.description}</ThemedText>
                </View>
                <View style={styles.lineItemDetails}>
                  <ThemedText style={[styles.lineItemText, { color: mutedColor }]}>
                    {item.quantity.toFixed(2)} {item.unit} × ₱{item.unitPrice.toFixed(2)}
                  </ThemedText>
                  <ThemedText style={[styles.lineItemAmount, { color: textColor }]}>
                    ₱{item.amount.toFixed(2)}
                  </ThemedText>
                </View>
              </View>
            ))}

            {/* Totals */}
            <View style={styles.totalsSection}>
              <View style={styles.totalRow}>
                <ThemedText style={[styles.totalLabel, { color: mutedColor }]}>Original Amount:</ThemedText>
                <ThemedText style={styles.totalValue}>₱{receipt.totalAmount.toFixed(2)}</ThemedText>
              </View>

              {isRefunded && (
                <>
                  <View style={styles.totalRow}>
                    <ThemedText style={[styles.totalLabel, { color: dangerColor }]}>Less Refund:</ThemedText>
                    <ThemedText style={[styles.totalValue, { color: dangerColor }]}>
                      -₱{receipt.payment.refundAmount?.toFixed(2)}
                    </ThemedText>
                  </View>
                  <View style={[styles.grandTotal, { borderTopColor: borderColor }]}>
                    <ThemedText style={styles.grandTotalLabel}>NET AMOUNT:</ThemedText>
                    <ThemedText style={[styles.grandTotalValue, { color: secondaryColor }]}>
                      ₱{netAmount.toFixed(2)}
                    </ThemedText>
                  </View>
                </>
              )}

              {!isRefunded && (
                <View style={[styles.grandTotal, { borderTopColor: borderColor }]}>
                  <ThemedText style={styles.grandTotalLabel}>TOTAL PAID:</ThemedText>
                  <ThemedText style={[styles.grandTotalValue, { color: secondaryColor }]}>
                    ₱{receipt.totalAmount.toFixed(2)}
                  </ThemedText>
                </View>
              )}
            </View>
          </View>

          {/* Payment Information */}
          <View style={[styles.card, { backgroundColor: cardColor, borderColor: borderColor }]}>
            <View style={styles.cardHeader}>
              <DollarSign size={20} color={primaryColor} />
              <ThemedText style={styles.cardTitle}>Payment Information</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Method:</ThemedText>
              <ThemedText style={styles.infoValue}>{receipt.payment.paymentMethod}</ThemedText>
            </View>
            <View style={styles.infoRow}>
              <ThemedText style={[styles.infoLabel, { color: mutedColor }]}>Status:</ThemedText>
              <View style={[
                styles.statusBadge,
                { backgroundColor: isRefunded ? dangerColor : secondaryColor }
              ]}>
                <Text style={styles.statusBadgeText}>{receipt.payment.status}</Text>
              </View>
            </View>
          </View>

          {/* Open in Browser Button */}
          <TouchableOpacity
            style={[styles.browserButton, { backgroundColor: primaryColor }]}
            onPress={handleOpenInBrowser}
          >
            <ExternalLink size={20} color="#ffffff" />
            <Text style={styles.browserButtonText}>Open Printable Version</Text>
          </TouchableOpacity>

          {/* Footer */}
          <View style={styles.footer}>
            <CheckCircle size={32} color={secondaryColor} />
            <ThemedText style={[styles.footerText, { color: mutedColor }]}>
              Thank you for your business!
            </ThemedText>
            <ThemedText style={[styles.footerSubtext, { color: mutedColor }]}>
              This is an official receipt generated by the Autonomous Laundry System.
            </ThemedText>
          </View>
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
  receiptNumber: {
    fontSize: 18,
    fontWeight: '600',
  },
  refundBanner: {
    margin: 16,
    padding: 16,
    borderRadius: 12,
    borderWidth: 2,
  },
  refundHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 12,
  },
  refundTitle: {
    fontSize: 16,
    fontWeight: 'bold',
    marginLeft: 8,
  },
  refundDetails: {
    marginLeft: 32,
  },
  refundText: {
    fontSize: 14,
    marginBottom: 4,
  },
  card: {
    margin: 16,
    marginTop: 0,
    padding: 20,
    borderRadius: 16,
    borderWidth: 1,
  },
  cardHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  cardTitle: {
    fontSize: 18,
    fontWeight: '600',
    marginLeft: 8,
  },
  infoRow: {
    flexDirection: 'row',
    marginBottom: 12,
    alignItems: 'flex-start',
  },
  infoLabel: {
    fontSize: 14,
    width: 120,
    fontWeight: '500',
  },
  infoValue: {
    fontSize: 14,
    flex: 1,
  },
  lineItem: {
    paddingVertical: 16,
    borderBottomWidth: 1,
  },
  lineItemDescription: {
    marginBottom: 8,
  },
  lineItemTitle: {
    fontSize: 15,
    fontWeight: '500',
  },
  lineItemDetails: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  lineItemText: {
    fontSize: 13,
  },
  lineItemAmount: {
    fontSize: 16,
    fontWeight: '600',
  },
  totalsSection: {
    marginTop: 16,
    paddingTop: 16,
  },
  totalRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginBottom: 8,
  },
  totalLabel: {
    fontSize: 15,
  },
  totalValue: {
    fontSize: 15,
    fontWeight: '500',
  },
  grandTotal: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 12,
    paddingTop: 12,
    borderTopWidth: 2,
  },
  grandTotalLabel: {
    fontSize: 18,
    fontWeight: 'bold',
  },
  grandTotalValue: {
    fontSize: 20,
    fontWeight: 'bold',
  },
  statusBadge: {
    paddingHorizontal: 12,
    paddingVertical: 4,
    borderRadius: 12,
  },
  statusBadgeText: {
    color: '#ffffff',
    fontSize: 13,
    fontWeight: '600',
  },
  browserButton: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    margin: 16,
    marginTop: 8,
    padding: 16,
    borderRadius: 12,
  },
  browserButtonText: {
    color: '#ffffff',
    fontSize: 16,
    fontWeight: '600',
    marginLeft: 8,
  },
  footer: {
    alignItems: 'center',
    padding: 32,
    paddingTop: 16,
  },
  footerText: {
    fontSize: 16,
    marginTop: 12,
    textAlign: 'center',
  },
  footerSubtext: {
    fontSize: 12,
    marginTop: 8,
    textAlign: 'center',
    lineHeight: 18,
  },
  loadingState: {
    alignItems: 'center',
    justifyContent: 'center',
    padding: 48,
    marginTop: 50,
  },
  loadingText: {
    fontSize: 16,
    marginTop: 16,
    textAlign: 'center',
  },
});
