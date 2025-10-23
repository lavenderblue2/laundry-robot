import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';

const API_BASE_URL = 'https://laundry.nexusph.site/api';

// Helper function to get auth headers
async function getAuthHeaders() {
        try {
                const token = await AsyncStorage.getItem('jwt_token');
                if (token) {
                        return {
                                'Authorization': `Bearer ${token}`,
                                'Content-Type': 'application/json'
                        };
                }
        } catch (error) {
                console.warn('Failed to retrieve JWT token from AsyncStorage:', error);
        }
        return {
                'Content-Type': 'application/json'
        };
}

// Handle 401 responses by clearing tokens and redirecting to login
async function handle401() {
        console.log('🚨 handle401 CALLED - clearing tokens and redirecting');
        try {
                await AsyncStorage.removeItem('jwt_token');
                await AsyncStorage.removeItem('user_data');
                console.log('🚨 Token cleared due to 401 response');

                // Navigate to login
                const { router } = require('expo-router');
                console.log('🚨 Navigating to /auth/login');
                router.replace('/auth/login');
        } catch (error) {
                console.warn('❌ Failed to clear auth data on 401:', error);
        }
}

// API functions that handle JWT tokens and 401 responses
export const apiGet = async (endpoint: string, config?: any) => {
        console.log(`📡 apiGet CALLED: ${endpoint}`);
        try {
                const headers = await getAuthHeaders();
                console.log(`📡 Making GET request to: ${API_BASE_URL}${endpoint}`);
                const response = await axios.get(`${API_BASE_URL}${endpoint}`, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                console.log(`✅ apiGet SUCCESS: ${endpoint}`);
                return response;
        } catch (error: any) {
                console.log(`❌ apiGet ERROR: ${endpoint}, status:`, error.response?.status);
                if (error.response?.status === 401) {
                        console.log(`🚨 401 detected in apiGet: ${endpoint}`);
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

export const apiPost = async (endpoint: string, data?: any, config?: any) => {
        try {
                const headers = await getAuthHeaders();
                const response = await axios.post(`${API_BASE_URL}${endpoint}`, data, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                return response;
        } catch (error: any) {
                if (error.response?.status === 401) {
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

export const apiPut = async (endpoint: string, data?: any, config?: any) => {
        console.log(`📡 apiPut CALLED: ${endpoint}`, data);
        try {
                const headers = await getAuthHeaders();
                console.log(`📡 Making PUT request to: ${API_BASE_URL}${endpoint}`);
                const response = await axios.put(`${API_BASE_URL}${endpoint}`, data, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                console.log(`✅ apiPut SUCCESS: ${endpoint}`, response.data);
                return response;
        } catch (error: any) {
                console.log(`❌ apiPut ERROR: ${endpoint}`, error.response?.status, error.response?.data);
                if (error.response?.status === 401) {
                        console.log(`🚨 401 detected in apiPut: ${endpoint}`);
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

export const apiDelete = async (endpoint: string, config?: any) => {
        try {
                const headers = await getAuthHeaders();
                const response = await axios.delete(`${API_BASE_URL}${endpoint}`, {
                        ...config,
                        headers: { ...headers, ...(config?.headers || {}) },
                        timeout: 10000
                });
                return response;
        } catch (error: any) {
                if (error.response?.status === 401) {
                        await handle401();
                        return null; // Return null for 401 errors after handling
                }
                throw error; // Only throw for non-401 errors
        }
};

// For backward compatibility - create a basic axios instance without interceptors
export const api = axios.create({
        baseURL: API_BASE_URL,
        timeout: 10000,
        headers: {
                'Content-Type': 'application/json',
        },
});