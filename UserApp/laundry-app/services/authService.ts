import AsyncStorage from '@react-native-async-storage/async-storage';
import { apiPost, apiGet } from './api';

export interface LoginRequest {
  username: string;
  password: string;
}

export interface AuthResponse {
  token: string;
  customerId: string;
  customerName: string;
  expiresAt: string;
}

export interface User {
  customerId: string;
  customerName: string;
  assignedBeaconId?: number;
  roomNumber?: string;
  roomDescription?: string;
}

export const authService = {
  async login(data: LoginRequest): Promise<AuthResponse> {
    const response = await apiPost('/auth/login', data);
    const authData = response.data;
    
    // Store token and user data securely
    try {
      await AsyncStorage.setItem('jwt_token', authData.token);
      await AsyncStorage.setItem('user_data', JSON.stringify({
        customerId: authData.customerId,
        customerName: authData.customerName,
      }));
    } catch (error) {
      console.error('Failed to store auth data in AsyncStorage:', error);
      throw new Error('Authentication data could not be stored securely');
    }
    
    return authData;
  },

  async logout(): Promise<void> {
    try {
      await AsyncStorage.removeItem('jwt_token');
      await AsyncStorage.removeItem('user_data');
    } catch (error) {
      console.warn('Failed to clear auth data from AsyncStorage during logout:', error);
    }
  },

  async getCurrentUser(): Promise<User | null> {
    try {
      const userData = await AsyncStorage.getItem('user_data');
      return userData ? JSON.parse(userData) : null;
    } catch (error) {
      console.warn('Failed to retrieve user data from AsyncStorage:', error);
      return null;
    }
  },

  async isLoggedIn(): Promise<boolean> {
    try {
      const token = await AsyncStorage.getItem('jwt_token');
      if (!token) {
        return false;
      }

      // Validate token with backend - the 401 handler will clear the token
      try {
        const response = await apiGet('/auth/generate200');
        return response !== null; // If response is null, it was a 401 (handled)
      } catch (error: any) {
        // For other errors (network, etc), assume logged in for offline support
        return true;
      }
    } catch (error) {
      console.warn('Failed to check login status from AsyncStorage:', error);
      return false;
    }
  },

  async getToken(): Promise<string | null> {
    try {
      return await AsyncStorage.getItem('jwt_token');
    } catch (error) {
      console.warn('Failed to retrieve token from AsyncStorage:', error);
      return null;
    }
  }
};