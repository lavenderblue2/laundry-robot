import { useFocusEffect, useRouter } from 'expo-router';
import React, { useCallback, useEffect, useState } from 'react';
import {
        ScrollView,
        StyleSheet,
        Text,
        TextInput,
        TouchableOpacity,
        View,
} from 'react-native';
import { useCustomAlert } from '../../components/CustomAlert';
import { ThemedText } from '../../components/ThemedText';
import { ThemedView } from '../../components/ThemedView';
import { useAuth } from '../../contexts/AuthContext';
import { useThemeColor } from '../../hooks/useThemeColor';
import { userService } from '../../services/api';

export default function ProfileScreen() {
        const router = useRouter();
        const { user, logout, refreshProfile } = useAuth();
        const { showAlert, AlertComponent } = useCustomAlert();
        const [isEditing, setIsEditing] = useState(false);
        const [firstName, setFirstName] = useState('');
        const [lastName, setLastName] = useState('');
        const [email, setEmail] = useState('');
        const [phone, setPhone] = useState('');
        const [isLoading, setIsLoading] = useState(false);

        const backgroundColor = useThemeColor({}, 'background');
        const textColor = useThemeColor({}, 'text');
        const primaryColor = useThemeColor({}, 'primary');
        const secondaryColor = useThemeColor({}, 'secondary');
        const cardColor = useThemeColor({}, 'card');
        const borderColor = useThemeColor({}, 'border');
        const mutedColor = useThemeColor({}, 'muted');
        const dangerColor = useThemeColor({}, 'danger');

        useEffect(() => {
                loadProfile();
        }, []);

        useFocusEffect(
                useCallback(() => {
                        refreshProfile();
                        loadProfile();
                }, []) // Remove refreshProfile from dependencies to prevent infinite loop
        );

        const loadProfile = async () => {
                try {
                        const profile = await userService.getProfile();
                        setFirstName(profile.firstName);
                        setLastName(profile.lastName);
                        setEmail(profile.email);
                        setPhone(profile.phone || '');
                } catch (error) {
                        // If profile doesn't exist, use user data from auth
                        if (user) {
                                const [first, ...lastParts] = user.customerName.split(' ');
                                setFirstName(first || '');
                                setLastName(lastParts.join(' ') || '');
                        }
                }
        };

        const handleSave = async () => {
                if (!firstName.trim() || !lastName.trim() || !email.trim()) {
                        showAlert('Error', 'Please fill in all required fields');
                        return;
                }

                setIsLoading(true);
                try {
                        await userService.updateProfile({
                                firstName: firstName.trim(),
                                lastName: lastName.trim(),
                                email: email.trim(),
                                phone: phone.trim() || undefined, // Don't send empty string
                        });

                        await refreshProfile(); // Refresh to show updated data
                        showAlert('Success', 'Profile updated successfully');
                        setIsEditing(false);
                } catch (error: any) {
                        showAlert('Error', error.response?.data?.message || 'Failed to update profile');
                } finally {
                        setIsLoading(false);
                }
        };

        const handleCancel = () => {
                setIsEditing(false);
                loadProfile(); // Reset to original values
        };

        const handleLogout = () => {
                showAlert(
                        'Logout',
                        'Are you sure you want to logout?',
                        [
                                { text: 'Cancel', style: 'cancel' },
                                {
                                        text: 'Logout',
                                        style: 'destructive',
                                        onPress: async () => {
                                                try {
                                                        await logout();
                                                } catch (error) {
                                                        console.error('Logout failed, but should have been handled:', error);
                                                        // If logout completely fails, show alert but don't prevent user from trying again
                                                        showAlert('Error', 'Logout encountered an issue, but auth data was cleared. Please restart the app if needed.');
                                                }
                                        }
                                },
                        ]
                );
        };

        return (
                <ThemedView style={styles.container}>
                        <ScrollView style={styles.scrollContainer}>
                                <View style={styles.header}>
                                        <View style={[styles.avatar, { backgroundColor: primaryColor }]}>
                                                <Text style={styles.avatarText}>
                                                        {firstName.charAt(0)}{lastName.charAt(0)}
                                                </Text>
                                        </View>
                                        <ThemedText style={styles.userName}>{user?.customerName || 'User'}</ThemedText>
                                        <ThemedText style={[styles.userId, { color: mutedColor }]}>ID: {user?.customerId || 'N/A'}</ThemedText>
                                </View>

                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <View style={styles.sectionHeader}>
                                                <ThemedText style={styles.sectionTitle}>Personal Information</ThemedText>
                                                {!isEditing && (
                                                        <TouchableOpacity onPress={() => setIsEditing(true)}>
                                                                <ThemedText style={[styles.editButton, { color: primaryColor }]}>Edit</ThemedText>
                                                        </TouchableOpacity>
                                                )}
                                        </View>

                                        <View style={styles.form}>
                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>First Name</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={firstName}
                                                                onChangeText={setFirstName}
                                                                placeholder="Enter your first name"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                autoCapitalize="words"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Last Name</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={lastName}
                                                                onChangeText={setLastName}
                                                                placeholder="Enter your last name"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                autoCapitalize="words"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Email</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={email}
                                                                onChangeText={setEmail}
                                                                placeholder="Enter your email"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                keyboardType="email-address"
                                                                autoCapitalize="none"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Phone Number</ThemedText>
                                                        <TextInput
                                                                style={[
                                                                        styles.input,
                                                                        { backgroundColor: isEditing ? cardColor : backgroundColor, borderColor: borderColor, color: textColor },
                                                                        !isEditing && { backgroundColor: backgroundColor }
                                                                ]}
                                                                value={phone}
                                                                onChangeText={setPhone}
                                                                placeholder="Enter your phone number"
                                                                placeholderTextColor={mutedColor}
                                                                editable={isEditing}
                                                                keyboardType="phone-pad"
                                                        />
                                                </View>

                                                <View style={styles.inputGroup}>
                                                        <ThemedText style={[styles.label, { color: textColor }]}>Assigned Room</ThemedText>
                                                        <View style={[styles.input, styles.readOnlyInput, { backgroundColor: backgroundColor, borderColor: borderColor }]}>
                                                                <ThemedText style={[styles.readOnlyText, { color: user?.roomName ? textColor : mutedColor }]}>
                                                                        {user?.roomName ? `📍 ${user.roomName}` : 'No room assigned'}
                                                                </ThemedText>
                                                        </View>
                                                        {user?.roomDescription && (
                                                                <ThemedText style={[styles.roomDescription, { color: mutedColor }]}>
                                                                        {user.roomDescription}
                                                                </ThemedText>
                                                        )}
                                                </View>

                                                {isEditing && (
                                                        <View style={styles.buttonRow}>
                                                                <TouchableOpacity
                                                                        style={[styles.button, styles.cancelButton, { backgroundColor: backgroundColor, borderColor: borderColor }]}
                                                                        onPress={handleCancel}
                                                                >
                                                                        <ThemedText style={[styles.cancelButtonText, { color: mutedColor }]}>Cancel</ThemedText>
                                                                </TouchableOpacity>
                                                                <TouchableOpacity
                                                                        style={[styles.button, { backgroundColor: isLoading ? mutedColor : primaryColor }]}
                                                                        onPress={handleSave}
                                                                        disabled={isLoading}
                                                                >
                                                                        <Text style={styles.saveButtonText}>
                                                                                {isLoading ? 'Saving...' : 'Save'}
                                                                        </Text>
                                                                </TouchableOpacity>
                                                        </View>
                                                )}
                                        </View>
                                </View>

                                <View style={[styles.section, { backgroundColor: cardColor }]}>
                                        <TouchableOpacity style={[styles.logoutButton, { backgroundColor: dangerColor }]} onPress={handleLogout}>
                                                <Text style={styles.logoutButtonText}>Logout</Text>
                                        </TouchableOpacity>
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
        scrollContainer: {
                flex: 1,
        },
        header: {
                alignItems: 'center',
                padding: 32,
                paddingTop: 60,
        },
        avatar: {
                width: 80,
                height: 80,
                borderRadius: 40,
                alignItems: 'center',
                justifyContent: 'center',
                marginBottom: 16,
        },
        avatarText: {
                fontSize: 28,
                fontWeight: 'bold',
                color: '#ffffff',
        },
        userName: {
                fontSize: 24,
                fontWeight: 'bold',
                marginBottom: 4,
        },
        userId: {
                fontSize: 14,
        },
        section: {
                marginHorizontal: 16,
                marginBottom: 16,
                borderRadius: 12,
                padding: 20,
        },
        sectionHeader: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: 20,
        },
        sectionTitle: {
                fontSize: 18,
                fontWeight: '600',
        },
        editButton: {
                fontSize: 16,
                fontWeight: '600',
        },
        form: {
                gap: 16,
        },
        inputGroup: {
                gap: 8,
        },
        label: {
                fontSize: 16,
                fontWeight: '600',
        },
        input: {
                borderWidth: 1,
                borderRadius: 8,
                padding: 16,
                fontSize: 16,
        },
        readOnlyInput: {
                justifyContent: 'center',
        },
        readOnlyText: {
                fontSize: 16,
        },
        roomDescription: {
                fontSize: 14,
                marginTop: 4,
                fontStyle: 'italic',
        },
        buttonRow: {
                flexDirection: 'row',
                gap: 12,
                marginTop: 8,
        },
        button: {
                flex: 1,
                borderRadius: 8,
                padding: 14,
                alignItems: 'center',
        },
        cancelButton: {
                borderWidth: 1,
        },
        cancelButtonText: {
                fontSize: 16,
                fontWeight: '600',
        },
        saveButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        menuItem: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
                paddingVertical: 16,
                borderBottomWidth: 1,
        },
        menuItemText: {
                fontSize: 16,
        },
        menuItemArrow: {
                fontSize: 18,
        },
        logoutButton: {
                borderRadius: 8,
                padding: 16,
                alignItems: 'center',
        },
        logoutButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
});