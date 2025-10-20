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

/**
 * Get list of all administrators - HARDCODED like the web fix
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
