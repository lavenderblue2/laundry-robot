import AsyncStorage from '@react-native-async-storage/async-storage';
import axios from 'axios';
import * as FileSystem from 'expo-file-system';

const API_BASE_URL = 'https://laundry.nexusph.site/api';

export interface Message {
  id: number;
  senderId: string;
  senderName: string;
  senderType: 'Customer' | 'Admin';
  content: string;
  sentAt: string;
  isRead: boolean;
  readAt?: string;
  imageUrl?: string;
  requestId?: number;
}

export interface SendMessageParams {
  content?: string;
  imageUri?: string;
  requestId?: number;
}

/**
 * Get all messages for the authenticated customer - HARDCODED like userService
 */
export const getMessages = async (lastMessageId?: number, limit: number = 50): Promise<Message[]> => {
  try {
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    let url = `${API_BASE_URL}/messages?limit=${limit}`;
    if (lastMessageId) {
      url += `&lastMessageId=${lastMessageId}`;
    }

    console.log('🔵 Fetching messages from:', url);

    const response = await axios.get(url, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });

    console.log('✅ Messages loaded:', response.data.length);
    return response.data;
  } catch (error: any) {
    console.error('❌ Error fetching messages:', error.response?.data || error.message);
    throw error;
  }
};

/**
 * Get unread message count - HARDCODED
 */
export const getUnreadCount = async (): Promise<number> => {
  try {
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const response = await axios.get(`${API_BASE_URL}/messages/unread-count`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });

    return response.data.unreadCount;
  } catch (error: any) {
    console.error('Error fetching unread count:', error.response?.data || error.message);
    throw error;
  }
};

/**
 * Send a message to admin - HARDCODED
 */
export const sendMessage = async ({ content, imageUri, requestId }: SendMessageParams): Promise<Message> => {
  try {
    if (!content && !imageUri) {
      throw new Error('Message content or image is required');
    }

    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    const formData = new FormData();

    if (content) {
      formData.append('content', content);
    }

    if (imageUri) {
      // Get file info
      const fileInfo = await FileSystem.getInfoAsync(imageUri);
      if (!fileInfo.exists) {
        throw new Error('Image file not found');
      }

      // Extract filename and type
      const filename = imageUri.split('/').pop() || 'image.jpg';
      const match = /\.(\w+)$/.exec(filename);
      const type = match ? `image/${match[1]}` : 'image/jpeg';

      formData.append('image', {
        uri: imageUri,
        name: filename,
        type
      } as any);
    }

    if (requestId) {
      formData.append('requestId', requestId.toString());
    }

    console.log('🔵 Sending message to:', `${API_BASE_URL}/messages`);

    const response = await axios.post(`${API_BASE_URL}/messages`, formData, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'multipart/form-data',
      },
      timeout: 10000
    });

    console.log('✅ Message sent:', response.data.id);
    return response.data;
  } catch (error: any) {
    console.error('❌ Error sending message:', error.response?.data || error.message);
    throw error;
  }
};

/**
 * Mark messages as read - HARDCODED
 */
export const markMessagesAsRead = async (messageIds: number[]): Promise<void> => {
  try {
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    await axios.post(`${API_BASE_URL}/messages/mark-read`, messageIds, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });
  } catch (error: any) {
    console.error('Error marking messages as read:', error.response?.data || error.message);
    throw error;
  }
};

/**
 * Delete a message - HARDCODED
 */
export const deleteMessage = async (messageId: number): Promise<void> => {
  try {
    const token = await AsyncStorage.getItem('jwt_token');
    if (!token) {
      throw new Error('Authentication required');
    }

    await axios.delete(`${API_BASE_URL}/messages/${messageId}`, {
      headers: {
        'Authorization': `Bearer ${token}`,
        'Content-Type': 'application/json'
      },
      timeout: 10000
    });
  } catch (error: any) {
    console.error('Error deleting message:', error.response?.data || error.message);
    throw error;
  }
};
