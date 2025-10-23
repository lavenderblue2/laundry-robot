import { apiGet, apiPut, apiDelete } from './api';
import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

export interface Admin {
  id: string;
  firstName: string;
  lastName: string;
  fullName: string;
  email: string | null;
  isActive: boolean;
}

export interface UserProfile {
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  roomName?: string;
  roomDescription?: string;
  assignedBeaconMacAddress?: string;
}

export interface UpdateProfileRequest {
  firstName?: string;
  lastName?: string;
  email?: string;
  phone?: string;
  address?: string;
  roomNumber?: string;
  roomDescription?: string;
}

export interface NotificationSettings {
  notificationsEnabled: boolean;
  vibrationEnabled: boolean;
  robotArrivalEnabled: boolean;
  robotDeliveryEnabled: boolean;
  messagesEnabled: boolean;
  statusChangesEnabled: boolean;
  robotArrivalSound: string;
  messageSound: string;
}

/**
 * Get list of all administrators - HARDCODED URL like the web fix
 */
export const getAdmins = async (): Promise<Admin[]> => {
  try {
    // Get JWT token
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      console.error('No JWT token found');
      throw new Error('Authentication required');
    }

    console.log('🔵 Fetching admins from: https://laundry.nexusph.site/api/user/admins');

    // HARDCODED URL - same fix as web dashboard
    const response = await axios.get('https://laundry.nexusph.site/api/user/admins', {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });

    console.log('✅ Admins loaded successfully:', response.data.length);
    return response.data;
  } catch (error: any) {
    console.error('❌ Error fetching admins:', error.response?.data || error.message);
    console.error('Status:', error.response?.status);
    throw error;
  }
};

export const userService = {
  async getProfile(): Promise<UserProfile> {
    const response = await apiGet('/user/profile');
    return response.data;
  },

  async updateProfile(data: UpdateProfileRequest): Promise<{ success: boolean; message: string }> {
    
    const response = await apiPut('/user/profile', data);
    return response.data;
  },

  async getNotificationSettings(): Promise<NotificationSettings> {
    
    const response = await apiGet('/user/notifications');
    return response.data;
  },

  async updateNotificationSettings(settings: NotificationSettings): Promise<{ success: boolean; message: string }> {
    
    const response = await apiPut('/user/notifications', settings);
    return response.data;
  },

  async deleteAccount(): Promise<{ success: boolean; message: string }> {
    
    const response = await apiDelete('/user/account');
    return response.data;
  },

  async getLaundryHistory(page: number = 1, limit: number = 10): Promise<{
    requests: any[];
    total: number;
    page: number;
    totalPages: number;
  }> {
    
    const response = await apiGet(`/user/history?page=${page}&limit=${limit}`);
    return response.data;
  },

  async getLaundryStatistics(): Promise<{
    totalRequests: number;
    completedRequests: number;
    totalWeight: number;
    totalSpent: number;
    averageWeight: number;
    favoriteTimeSlot?: string;
    lastRequest?: string;
  }> {
    
    const response = await apiGet('/user/statistics');
    return response.data;
  }
};