import { useRouter } from 'expo-router';
import React, { useState } from 'react';
import {
        KeyboardAvoidingView,
        Platform,
        ScrollView,
        StyleSheet,
        Text,
        TextInput,
        TouchableOpacity,
        View,
} from 'react-native';
import { useAuth } from '../../contexts/AuthContext';
import { useThemeColor } from '../../hooks/useThemeColor';
import { ThemedView } from '../../components/ThemedView';
import { ThemedText } from '../../components/ThemedText';
import { useCustomAlert } from '../../components/CustomAlert';

export default function LoginScreen() {
        const [username, setUsername] = useState('');
        const [password, setPassword] = useState('');
        const [isLoading, setIsLoading] = useState(false);
        const { login } = useAuth();
        const { showAlert, AlertComponent } = useCustomAlert();
        const router = useRouter();
        
        const backgroundColor = useThemeColor({}, 'background');
        const textColor = useThemeColor({}, 'text');
        const primaryColor = useThemeColor({}, 'primary');
        const cardColor = useThemeColor({}, 'card');
        const borderColor = useThemeColor({}, 'border');
        const mutedColor = useThemeColor({}, 'muted');

        const handleLogin = async () => {
                if (!username.trim() || !password.trim()) {
                        showAlert('Error', 'Please fill in all fields');
                        return;
                }

                setIsLoading(true);
                try {
                        await login(username.trim(), password.trim());
                        router.replace('/(tabs)');
                } catch (error: any) {
                        showAlert('Login Failed', error.response?.data?.message || 'Please check your credentials');
                } finally {
                        setIsLoading(false);
                }
        };

        return (
                <ThemedView style={styles.container}>
                        <KeyboardAvoidingView
                                style={styles.keyboardContainer}
                                behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
                        >
                                <ScrollView contentContainerStyle={styles.scrollContent}>
                                        <View style={styles.header}>
                                                <ThemedText style={styles.title}>Laundry Service</ThemedText>
                                                <ThemedText style={[styles.subtitle, { color: mutedColor }]}>Welcome back</ThemedText>
                                        </View>

                                        <View style={styles.form}>
                                        <View style={styles.inputGroup}>
                                                <ThemedText style={[styles.label, { color: textColor }]}>Username</ThemedText>
                                                <TextInput
                                                        style={[styles.input, { backgroundColor: cardColor, borderColor: borderColor, color: textColor }]}
                                                        value={username}
                                                        onChangeText={setUsername}
                                                        placeholder="Enter your username"
                                                        placeholderTextColor={mutedColor}
                                                        autoCapitalize="none"
                                                        autoCorrect={false}
                                                />
                                        </View>

                                        <View style={styles.inputGroup}>
                                                <ThemedText style={[styles.label, { color: textColor }]}>Password</ThemedText>
                                                <TextInput
                                                        style={[styles.input, { backgroundColor: cardColor, borderColor: borderColor, color: textColor }]}
                                                        value={password}
                                                        onChangeText={setPassword}
                                                        placeholder="Enter your password"
                                                        placeholderTextColor={mutedColor}
                                                        secureTextEntry={true}
                                                        autoCapitalize="none"
                                                        autoCorrect={false}
                                                />
                                        </View>

                                        <TouchableOpacity
                                                style={[styles.loginButton, { backgroundColor: isLoading ? mutedColor : primaryColor }]}
                                                onPress={handleLogin}
                                                disabled={isLoading}
                                        >
                                                <Text style={styles.loginButtonText}>
                                                        {isLoading ? 'Signing In...' : 'Sign In'}
                                                </Text>
                                        </TouchableOpacity>
                                </View>
                                </ScrollView>
                        </KeyboardAvoidingView>
                        <AlertComponent />
                </ThemedView>
        );
}

const styles = StyleSheet.create({
        container: {
                flex: 1,
        },
        keyboardContainer: {
                flex: 1,
        },
        scrollContent: {
                flexGrow: 1,
                justifyContent: 'center',
                padding: 24,
        },
        header: {
                alignItems: 'center',
                marginBottom: 48,
        },
        title: {
                fontSize: 32,
                fontWeight: 'bold',
                marginBottom: 8,
        },
        subtitle: {
                fontSize: 16,
        },
        form: {
                gap: 24,
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
        loginButton: {
                borderRadius: 8,
                padding: 16,
                alignItems: 'center',
                marginTop: 8,
        },
        loginButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
});