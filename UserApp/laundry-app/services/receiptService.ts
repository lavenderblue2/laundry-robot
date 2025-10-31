import { apiGet } from './api';

export interface ReceiptLineItem {
  description: string;
  quantity: number;
  unit: string;
  unitPrice: number;
  amount: number;
}

export interface Receipt {
  id: number;
  receiptNumber: string;
  generatedAt: string;
  business: {
    businessName: string;
    businessAddress: string;
    businessPhone: string;
    businessEmail: string;
    taxIdentificationNumber?: string;
  };
  customer: {
    customerName: string;
    customerPhone: string;
    customerAddress: string;
  };
  lineItems: ReceiptLineItem[];
  subtotal: number;
  taxAmount: number;
  totalAmount: number;
  payment: {
    paymentMethod: string;
    transactionId: string;
    paymentReference?: string;
    status: string;
    refundAmount?: number;
    refundedAt?: string;
    refundReason?: string;
  };
}

export const receiptService = {
  /**
   * Get receipt by request ID
   */
  async getReceiptByRequest(requestId: number): Promise<Receipt | null> {
    try {
      const response = await apiGet(`/Receipt/by-request/${requestId}`);

      if (response && response.data && response.data.id) {
        // Fetch full receipt details
        return await this.getReceipt(response.data.id);
      }

      return null;
    } catch (error: any) {
      if (error.response?.status === 404) {
        return null; // Receipt not generated yet
      }
      throw error;
    }
  },

  /**
   * Get receipt by ID
   */
  async getReceipt(receiptId: number): Promise<Receipt> {
    const response = await apiGet(`/Receipt/view/${receiptId}`);
    if (!response) {
      throw new Error('Failed to fetch receipt');
    }
    return response.data;
  },

  /**
   * Get web URL for receipt (for opening in browser)
   */
  getReceiptWebUrl(receiptId: number): string {
    // Use the production URL
    const baseUrl = 'https://laundry.nexusph.site';
    return `${baseUrl}/Accounting/ViewReceiptPrint/${receiptId}`;
  },
};
