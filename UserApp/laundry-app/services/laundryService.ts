import { apiGet, apiPost, apiPut } from './api';

export enum LaundryRequestType {
  Pickup = 0,
  Delivery = 1,
  PickupAndDelivery = 2
}

export enum LaundryRequestStatus {
  Pending = 'Pending',
  Accepted = 'Accepted',
  InProgress = 'InProgress', 
  RobotEnRoute = 'RobotEnRoute',
  ArrivedAtRoom = 'ArrivedAtRoom',
  LaundryLoaded = 'LaundryLoaded',
  ReturnedToBase = 'ReturnedToBase',
  WeighingComplete = 'WeighingComplete',
  PaymentPending = 'PaymentPending',
  Completed = 'Completed',
  Declined = 'Declined',
  Cancelled = 'Cancelled',
  Washing = 'Washing',
  FinishedWashing = 'FinishedWashing',
  FinishedWashingGoingToRoom = 'FinishedWashingGoingToRoom',
  FinishedWashingArrivedAtRoom = 'FinishedWashingArrivedAtRoom',
  FinishedWashingGoingToBase = 'FinishedWashingGoingToBase',
  FinishedWashingAwaitingPickup = 'FinishedWashingAwaitingPickup'
}

export interface LaundryRequest {
  // Simplified request - only essential fields
}

export interface LaundryRequestResponse {
  id: number;
  customerId: string;
  customerName: string;
  customerPhone: string;
  address: string;
  instructions?: string;
  type: LaundryRequestType;
  status: LaundryRequestStatus;
  weight?: number;
  totalCost?: number;
  pricePerKg?: number;
  requestedAt: string;
  scheduledAt: string;
  acceptedAt?: string;
  completedAt?: string;
  assignedRobot?: string;
  declineReason?: string;
  assignedBeaconMacAddress?: string;
  estimatedArrival?: string;
  actualArrival?: string;
  arrivedAtRoomAt?: string;
}

export interface WeightConfirmation {
  requestId: number;
  weight: number;
  totalCost: number;
  pricePerKg: number;
  minimumCharge?: number;
}

export interface PaymentConfirmation {
  requestId: number;
  paymentMethod: 'Cash' | 'Card' | 'DigitalWallet' | 'BankTransfer';
  paymentReference?: string;
  notes?: string;
}

export const laundryService = {
  async createRequest(): Promise<{ id: number; status: string; message: string }> {
    console.log('Creating request...');
    try {
      const response = await apiPost('/requests', {});
      console.log('Request creation successful:', response.data);
      return response.data;
    } catch (error: any) {
      console.error('Request creation failed:', error.response?.status, error.response?.data);
      throw error;
    }
  },

  async getRequestStatus(requestId: number): Promise<LaundryRequestResponse> {
    const response = await apiGet(`/requests/status/${requestId}`);
    return response.data;
  },

  async getUserRequests(): Promise<LaundryRequestResponse[]> {
    const response = await apiGet('/requests/my-requests');
    if (!response) return []; // Handle 401 gracefully
    return response.data;
  },

  async getActiveRequest(): Promise<LaundryRequestResponse | null> {
    const response = await apiGet('/requests/active');
    if (!response) return null; // Handle 401 gracefully
    return response.data || null;
  },

  async confirmLaundryLoaded(requestId: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-loaded`, {});
    return response.data;
  },

  async confirmLaundryLoadedWithWeight(requestId: number, weight: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-loaded`, { weight });
    return response.data;
  },

  async confirmLaundryUnloaded(requestId: number): Promise<{ success: boolean; message: string }> {
    const response = await apiPost(`/requests/${requestId}/confirm-unloaded`, {});
    return response.data;
  },

  async selectDeliveryOption(requestId: number, deliveryType: 'Delivery' | 'Pickup'): Promise<{ success: boolean; message: string; status: string }> {
    const response = await apiPost(`/requests/${requestId}/select-delivery-option`, { deliveryType });
    return response.data;
  },

  async confirmWeightAndCost(requestId: number): Promise<WeightConfirmation> {
    
    const response = await apiPost(`/requests/${requestId}/confirm-weight`, {});
    return response.data;
  },

  async confirmPayment(requestId: number, paymentData: PaymentConfirmation): Promise<{ success: boolean; message: string }> {
    
    const response = await apiPost(`/requests/${requestId}/confirm-payment`, paymentData);
    return response.data;
  },

  async cancelRequest(requestId: number, reason?: string): Promise<{ success: boolean; message: string }> {
    
    const response = await apiPost(`/requests/${requestId}/cancel`, { reason });
    return response.data;
  },

  async getRatesAndPricing(): Promise<{
    pricePerKg: number;
    minimumCharge: number;
    currency: string;
    effectiveFrom: string;
  }> {
    
    const response = await apiGet('/requests/pricing');
    return response.data;
  },

  async trackRobot(requestId: number): Promise<{
    robotName: string;
    currentLocation?: string;
    batteryLevel?: number;
    estimatedArrival?: string;
    status: 'idle' | 'enroute' | 'arrived' | 'returning' | 'maintenance';
    lastUpdate: string;
  }> {

    const response = await apiGet(`/requests/${requestId}/robot-status`);
    return response.data;
  },

  async getAvailableRobots(): Promise<{
    totalRobots: number;
    availableRobots: number;
    busyRobots: number;
    offlineRobots: number;
    timestamp: string;
  }> {
    const response = await apiGet('/requests/available-robots');
    return response.data;
  }
};