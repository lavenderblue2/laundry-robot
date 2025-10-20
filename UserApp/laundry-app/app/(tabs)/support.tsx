import React, { useState, useEffect, useRef } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TextInput,
  TouchableOpacity,
  Image,
  ActivityIndicator,
  Alert,
  KeyboardAvoidingView,
  Platform,
} from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { Message, getMessages, sendMessage, markMessagesAsRead, getUnreadCount } from '../../services/messageService';
import { Admin, getAdmins } from '../../services/userService';

export default function SupportScreen() {
  const [view, setView] = useState<'adminList' | 'chat'>('adminList');
  const [admins, setAdmins] = useState<Admin[]>([]);
  const [selectedAdmin, setSelectedAdmin] = useState<Admin | null>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [messageText, setMessageText] = useState('');
  const [selectedImage, setSelectedImage] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [sending, setSending] = useState(false);
  const [unreadCount, setUnreadCount] = useState(0);
  const flatListRef = useRef<FlatList>(null);
  const pollIntervalRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    if (view === 'adminList') {
      loadAdmins();
    }
  }, [view]);

  useEffect(() => {
    if (view === 'chat') {
      loadMessages();
      loadUnreadCount();

    // Poll for new messages every 5 seconds
    pollIntervalRef.current = setInterval(() => {
      pollNewMessages();
    }, 5000);

      return () => {
        if (pollIntervalRef.current) {
          clearInterval(pollIntervalRef.current);
        }
      };
    }
  }, [view]);

  const loadAdmins = async () => {
    try {
      setLoading(true);
      const data = await getAdmins();
      setAdmins(data);
    } catch (error: any) {
      console.error('Error loading admins:', error);
      const errorMsg = error.response?.data?.message || error.message || 'Unknown error';
      Alert.alert('Error', `Failed to load administrators: ${errorMsg}`);
    } finally {
      setLoading(false);
    }
  };

  const handleAdminSelect = (admin: Admin) => {
    setSelectedAdmin(admin);
    setView('chat');
  };

  const handleBackToList = () => {
    setView('adminList');
    setMessages([]);
    if (pollIntervalRef.current) {
      clearInterval(pollIntervalRef.current);
    }
  };

  const loadMessages = async () => {
    try {
      setLoading(true);
      const data = await getMessages();
      setMessages(data);

      // Mark admin messages as read
      const unreadAdminMessages = data.filter(m => !m.isRead && m.senderType === 'Admin');
      if (unreadAdminMessages.length > 0) {
        await markMessagesAsRead(unreadAdminMessages.map(m => m.id));
        setUnreadCount(0);
      }
    } catch (error: any) {
      console.error('Error loading messages:', error);
      const errorMsg = error.response?.data?.message || error.message || 'Unknown error';
      Alert.alert('Error', `Failed to load messages: ${errorMsg}`);
    } finally {
      setLoading(false);
    }
  };

  const loadUnreadCount = async () => {
    try {
      const count = await getUnreadCount();
      setUnreadCount(count);
    } catch (error) {
      console.error('Error loading unread count:', error);
    }
  };

  const pollNewMessages = async () => {
    try {
      if (messages.length === 0) return;

      const lastMessageId = Math.max(...messages.map(m => m.id));
      const newMessages = await getMessages(lastMessageId);

      if (newMessages.length > 0) {
        setMessages(prev => [...prev, ...newMessages]);

        // Mark new admin messages as read
        const unreadAdminMessages = newMessages.filter(m => !m.isRead && m.senderType === 'Admin');
        if (unreadAdminMessages.length > 0) {
          await markMessagesAsRead(unreadAdminMessages.map(m => m.id));
        }

        // Scroll to bottom
        setTimeout(() => {
          flatListRef.current?.scrollToEnd({ animated: true });
        }, 100);
      }
    } catch (error) {
      console.error('Error polling messages:', error);
    }
  };

  const handleSendMessage = async () => {
    if (!messageText.trim() && !selectedImage) {
      return;
    }

    try {
      setSending(true);

      const newMessage = await sendMessage({
        content: messageText.trim() || undefined,
        imageUri: selectedImage || undefined,
      });

      setMessages(prev => [...prev, newMessage]);
      setMessageText('');
      setSelectedImage(null);

      // Scroll to bottom
      setTimeout(() => {
        flatListRef.current?.scrollToEnd({ animated: true });
      }, 100);
    } catch (error) {
      console.error('Error sending message:', error);
      Alert.alert('Error', 'Failed to send message. Please try again.');
    } finally {
      setSending(false);
    }
  };

  const handlePickImage = async () => {
    try {
      const permissionResult = await ImagePicker.requestMediaLibraryPermissionsAsync();

      if (!permissionResult.granted) {
        Alert.alert('Permission Required', 'Please grant permission to access photos');
        return;
      }

      const result = await ImagePicker.launchImageLibraryAsync({
        mediaTypes: ImagePicker.MediaTypeOptions.Images,
        allowsEditing: true,
        quality: 0.7,
        base64: false,
      });

      if (!result.canceled && result.assets[0]) {
        setSelectedImage(result.assets[0].uri);
      }
    } catch (error) {
      console.error('Error picking image:', error);
      Alert.alert('Error', 'Failed to pick image');
    }
  };

  const handleTakePhoto = async () => {
    try {
      const permissionResult = await ImagePicker.requestCameraPermissionsAsync();

      if (!permissionResult.granted) {
        Alert.alert('Permission Required', 'Please grant permission to access camera');
        return;
      }

      const result = await ImagePicker.launchCameraAsync({
        allowsEditing: true,
        quality: 0.7,
      });

      if (!result.canceled && result.assets[0]) {
        setSelectedImage(result.assets[0].uri);
      }
    } catch (error) {
      console.error('Error taking photo:', error);
      Alert.alert('Error', 'Failed to take photo');
    }
  };

  const handleImageOptions = () => {
    Alert.alert(
      'Attach Image',
      'Choose an option',
      [
        { text: 'Take Photo', onPress: handleTakePhoto },
        { text: 'Choose from Library', onPress: handlePickImage },
        { text: 'Cancel', style: 'cancel' },
      ]
    );
  };

  const renderMessage = ({ item }: { item: Message }) => {
    const isCustomer = item.senderType === 'Customer';
    const messageTime = new Date(item.sentAt).toLocaleTimeString([], {
      hour: '2-digit',
      minute: '2-digit',
    });

    return (
      <View style={[styles.messageContainer, isCustomer ? styles.customerMessage : styles.adminMessage]}>
        <View style={[styles.messageBubble, isCustomer ? styles.customerBubble : styles.adminBubble]}>
          <Text style={styles.senderName}>{item.senderName}</Text>

          {item.imageUrl && (
            <Image
              source={{ uri: `https://laundry.nexusph.site${item.imageUrl}` }}
              style={styles.messageImage}
              resizeMode="cover"
            />
          )}

          {item.content && (
            <Text style={[styles.messageText, isCustomer ? styles.customerText : styles.adminText]}>
              {item.content}
            </Text>
          )}

          <Text style={styles.messageTime}>{messageTime}</Text>
        </View>
      </View>
    );
  };

  const renderAdmin = ({ item }: { item: Admin }) => (
    <TouchableOpacity
      style={styles.adminCard}
      onPress={() => handleAdminSelect(item)}
    >
      <View style={styles.adminAvatar}>
        <Text style={styles.adminInitial}>
          {item.firstName.charAt(0).toUpperCase()}
        </Text>
      </View>
      <View style={styles.adminInfo}>
        <Text style={styles.adminName}>{item.fullName}</Text>
        {item.email && (
          <Text style={styles.adminEmail}>{item.email}</Text>
        )}
      </View>
      <Ionicons name="chevron-forward" size={24} color="#999" />
    </TouchableOpacity>
  );

  if (loading) {
    return (
      <View style={styles.centerContainer}>
        <ActivityIndicator size="large" color="#007AFF" />
        <Text style={styles.loadingText}>Loading...</Text>
      </View>
    );
  }

  // Admin List View
  if (view === 'adminList') {
    return (
      <View style={styles.container}>
        <View style={styles.header}>
          <View style={styles.headerIconContainer}>
            <Ionicons name="chatbubbles" size={28} color="#6366F1" />
          </View>
          <Text style={styles.headerTitle}>Contact Support</Text>
        </View>

        <FlatList
          data={admins}
          renderItem={renderAdmin}
          keyExtractor={(item) => item.id}
          contentContainerStyle={styles.adminList}
          ListEmptyComponent={
            <View style={styles.emptyContainer}>
              <View style={styles.emptyIconContainer}>
                <Ionicons name="person-outline" size={64} color="#475569" />
              </View>
              <Text style={styles.emptyText}>No administrators available</Text>
              <Text style={styles.emptySubtext}>Please check back later to contact support</Text>
            </View>
          }
        />
      </View>
    );
  }

  // Chat View
  return (
    <KeyboardAvoidingView
      style={styles.container}
      behavior={Platform.OS === 'ios' ? 'padding' : undefined}
      keyboardVerticalOffset={90}
    >
      <View style={styles.chatHeader}>
        <TouchableOpacity onPress={handleBackToList} style={styles.backButton}>
          <Ionicons name="arrow-back" size={24} color="#FFFFFF" />
        </TouchableOpacity>
        <View style={styles.chatHeaderInfo}>
          <Text style={styles.chatHeaderTitle}>
            {selectedAdmin?.fullName || 'Support'}
          </Text>
          <View style={styles.statusIndicator}>
            <View style={styles.statusDot} />
            <Text style={styles.statusText}>Active</Text>
          </View>
        </View>
        {unreadCount > 0 && (
          <View style={styles.unreadBadge}>
            <Text style={styles.unreadText}>{unreadCount}</Text>
          </View>
        )}
      </View>

      <FlatList
        ref={flatListRef}
        data={messages}
        renderItem={renderMessage}
        keyExtractor={(item) => item.id.toString()}
        contentContainerStyle={styles.messagesList}
        onContentSizeChange={() => flatListRef.current?.scrollToEnd({ animated: true })}
        ListEmptyComponent={
          <View style={styles.emptyContainer}>
            <Ionicons name="chatbubbles-outline" size={64} color="#ccc" />
            <Text style={styles.emptyText}>No messages yet</Text>
            <Text style={styles.emptySubtext}>Start a conversation with our support team</Text>
          </View>
        }
      />

      {selectedImage && (
        <View style={styles.imagePreview}>
          <Image source={{ uri: selectedImage }} style={styles.previewImage} />
          <TouchableOpacity style={styles.removeImageBtn} onPress={() => setSelectedImage(null)}>
            <Ionicons name="close-circle" size={24} color="#FF3B30" />
          </TouchableOpacity>
        </View>
      )}

      <View style={styles.inputContainer}>
        <TouchableOpacity style={styles.attachBtn} onPress={handleImageOptions}>
          <Ionicons name="image" size={24} color="#007AFF" />
        </TouchableOpacity>

        <TextInput
          style={styles.input}
          value={messageText}
          onChangeText={setMessageText}
          placeholder="Type a message..."
          multiline
          maxLength={500}
        />

        <TouchableOpacity
          style={[styles.sendBtn, sending && styles.sendBtnDisabled]}
          onPress={handleSendMessage}
          disabled={sending || (!messageText.trim() && !selectedImage)}
        >
          {sending ? (
            <ActivityIndicator size="small" color="white" />
          ) : (
            <Ionicons name="send" size={20} color="white" />
          )}
        </TouchableOpacity>
      </View>
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0F172A',
  },
  centerContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    backgroundColor: '#0F172A',
  },
  loadingText: {
    marginTop: 10,
    color: '#94A3B8',
    fontSize: 16,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 20,
    paddingTop: 60,
    backgroundColor: '#1E293B',
    borderBottomWidth: 1,
    borderBottomColor: '#334155',
  },
  headerIconContainer: {
    width: 48,
    height: 48,
    borderRadius: 24,
    backgroundColor: '#312E81',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 4,
  },
  emptyIconContainer: {
    width: 120,
    height: 120,
    borderRadius: 60,
    backgroundColor: '#1E293B',
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: 8,
  },
  chatHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 20,
    paddingTop: 60,
    backgroundColor: '#6366F1',
    borderBottomLeftRadius: 24,
    borderBottomRightRadius: 24,
    shadowColor: '#6366F1',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 8,
  },
  chatHeaderInfo: {
    flex: 1,
    marginLeft: 12,
  },
  chatHeaderTitle: {
    fontSize: 20,
    fontWeight: '700',
    color: '#FFFFFF',
    marginBottom: 4,
  },
  statusIndicator: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  statusDot: {
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#10B981',
  },
  statusText: {
    fontSize: 13,
    color: '#E0E7FF',
    fontWeight: '500',
  },
  backButton: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: 'rgba(255, 255, 255, 0.2)',
    justifyContent: 'center',
    alignItems: 'center',
  },
  adminList: {
    padding: 20,
    paddingTop: 24,
  },
  adminCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 18,
    backgroundColor: '#1E293B',
    borderRadius: 16,
    marginBottom: 16,
    borderWidth: 1,
    borderColor: '#334155',
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 4,
  },
  adminAvatar: {
    width: 56,
    height: 56,
    borderRadius: 28,
    backgroundColor: '#6366F1',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 16,
    shadowColor: '#6366F1',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.4,
    shadowRadius: 4,
    elevation: 3,
  },
  adminInitial: {
    color: 'white',
    fontSize: 22,
    fontWeight: '700',
  },
  adminInfo: {
    flex: 1,
  },
  adminName: {
    fontSize: 17,
    fontWeight: '600',
    color: '#F1F5F9',
    marginBottom: 6,
  },
  adminEmail: {
    fontSize: 14,
    color: '#94A3B8',
  },
  headerTitle: {
    fontSize: 24,
    fontWeight: '700',
    color: '#FFFFFF',
    marginLeft: 12,
    flex: 1,
  },
  unreadBadge: {
    backgroundColor: '#FF3B30',
    borderRadius: 12,
    minWidth: 24,
    height: 24,
    justifyContent: 'center',
    alignItems: 'center',
    paddingHorizontal: 6,
  },
  unreadText: {
    color: 'white',
    fontSize: 12,
    fontWeight: '600',
  },
  messagesList: {
    padding: 20,
    flexGrow: 1,
    backgroundColor: '#0F172A',
  },
  emptyContainer: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
    paddingTop: 100,
  },
  emptyText: {
    fontSize: 18,
    color: '#64748B',
    marginTop: 16,
    fontWeight: '600',
  },
  emptySubtext: {
    fontSize: 14,
    color: '#475569',
    marginTop: 8,
    textAlign: 'center',
    paddingHorizontal: 40,
  },
  messageContainer: {
    marginBottom: 16,
  },
  customerMessage: {
    alignItems: 'flex-end',
  },
  adminMessage: {
    alignItems: 'flex-start',
  },
  messageBubble: {
    maxWidth: '80%',
    padding: 14,
    borderRadius: 18,
  },
  customerBubble: {
    backgroundColor: '#6366F1',
    borderBottomRightRadius: 4,
    shadowColor: '#6366F1',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.3,
    shadowRadius: 4,
    elevation: 3,
  },
  adminBubble: {
    backgroundColor: '#1E293B',
    borderBottomLeftRadius: 4,
    borderWidth: 1,
    borderColor: '#334155',
  },
  senderName: {
    fontSize: 11,
    fontWeight: '600',
    marginBottom: 6,
    opacity: 0.9,
    color: '#E0E7FF',
  },
  messageImage: {
    width: 200,
    height: 200,
    borderRadius: 8,
    marginVertical: 8,
  },
  messageText: {
    fontSize: 15,
    lineHeight: 21,
  },
  customerText: {
    color: '#FFFFFF',
  },
  adminText: {
    color: '#F1F5F9',
  },
  messageTime: {
    fontSize: 10,
    marginTop: 6,
    opacity: 0.7,
    color: '#CBD5E1',
  },
  inputContainer: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    padding: 16,
    backgroundColor: '#1E293B',
    borderTopWidth: 1,
    borderTopColor: '#334155',
  },
  attachBtn: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: '#334155',
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: 12,
  },
  input: {
    flex: 1,
    backgroundColor: '#0F172A',
    borderRadius: 22,
    paddingHorizontal: 18,
    paddingVertical: 12,
    maxHeight: 100,
    fontSize: 15,
    color: '#F1F5F9',
    borderWidth: 1,
    borderColor: '#334155',
  },
  sendBtn: {
    backgroundColor: '#6366F1',
    width: 44,
    height: 44,
    borderRadius: 22,
    justifyContent: 'center',
    alignItems: 'center',
    marginLeft: 12,
    shadowColor: '#6366F1',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.4,
    shadowRadius: 4,
    elevation: 3,
  },
  sendBtnDisabled: {
    opacity: 0.4,
  },
  imagePreview: {
    backgroundColor: '#1E293B',
    borderTopWidth: 1,
    borderTopColor: '#334155',
    padding: 16,
  },
  previewImage: {
    width: 100,
    height: 100,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: '#334155',
  },
  removeImageBtn: {
    position: 'absolute',
    top: 12,
    right: 12,
    backgroundColor: '#EF4444',
    borderRadius: 12,
    width: 24,
    height: 24,
    justifyContent: 'center',
    alignItems: 'center',
  },
});
