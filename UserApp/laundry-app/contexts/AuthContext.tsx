import React, { createContext, useContext, useEffect, useState } from 'react';
import { authService } from '../services/authService';
import { userService } from '../services/userService';

interface User {
  customerId: string;
  customerName: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  phone?: string;
  roomName?: string;
  roomDescription?: string;
  assignedBeaconMacAddress?: string;
}

interface AuthContextType {
  user: User | null;
  isLoading: boolean;
  isLoggedIn: boolean;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  checkAuthStatus: () => Promise<void>;
  loadUserProfile: () => Promise<void>;
  refreshProfile: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};

interface AuthProviderProps {
  children: React.ReactNode;
}

export const AuthProvider: React.FC<AuthProviderProps> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  const login = async (username: string, password: string) => {
    try {
      const authResponse = await authService.login({ username, password });
      setUser({
        customerId: authResponse.customerId,
        customerName: authResponse.customerName,
      });
      setIsLoggedIn(true);
      // Load additional profile data
      await loadUserProfile();
    } catch (error) {
      console.error('Login error:', error);
      throw error;
    }
  };

  const logout = async () => {
    try {
      await authService.logout();
    } catch (error) {
      console.error('Logout storage clear error (but continuing with logout):', error);
      // Continue with logout even if storage clearing fails
    }
    
    // FORCE logout regardless of any errors above
    try {
      setUser(null);
      setIsLoggedIn(false);
      // Force navigation to index which will redirect to login
      const { router } = require('expo-router');
      router.replace('/');
    } catch (navigationError) {
      console.error('Navigation error during logout:', navigationError);
      // Even if navigation fails, user state is cleared
    }
  };

  const checkAuthStatus = async () => {
    console.log('🔍 checkAuthStatus CALLED');
    try {
      setIsLoading(true);
      console.log('🔍 Checking if user is logged in...');
      const isAuthenticated = await authService.isLoggedIn();
      console.log('🔍 isAuthenticated result:', isAuthenticated);
      
      if (isAuthenticated) {
        console.log('🔍 Getting current user...');
        const currentUser = await authService.getCurrentUser();
        console.log('🔍 currentUser:', currentUser);
        
        if (currentUser) {
          setUser(currentUser);
          setIsLoggedIn(true);
          // Load additional profile data
          console.log('🔍 Loading user profile...');
          await loadUserProfile();
        }
      }
    } catch (error) {
      console.error('❌ Auth check error:', error);
      setUser(null);
      setIsLoggedIn(false);
    } finally {
      console.log('🔍 checkAuthStatus FINISHED, setIsLoading(false)');
      setIsLoading(false);
    }
  };

  const loadUserProfile = async () => {
    console.log('👤 loadUserProfile CALLED');
    try {
      console.log('👤 Calling userService.getProfile()...');
      const profile = await userService.getProfile();
      console.log('👤 Profile received:', profile);
      
      setUser(prevUser => {
        console.log('👤 Setting user, prevUser:', prevUser);
        return prevUser ? {
          ...prevUser,
          firstName: profile.firstName,
          lastName: profile.lastName,
          email: profile.email,
          phone: profile.phone,
          roomName: profile.roomName,
          roomDescription: profile.roomDescription,
          assignedBeaconMacAddress: profile.assignedBeaconMacAddress,
        } : null;
      });
    } catch (error) {
      console.error('❌ Error loading user profile:', error);
    }
  };

  const refreshProfile = async () => {
    console.log('🔄 refreshProfile CALLED, user:', !!user, 'isLoggedIn:', isLoggedIn);
    if (user && isLoggedIn) {
      await loadUserProfile();
    }
  };

  useEffect(() => {
    console.log('⚡ AuthContext useEffect TRIGGERED - calling checkAuthStatus');
    checkAuthStatus();
  }, []);

  const value: AuthContextType = {
    user,
    isLoading,
    isLoggedIn,
    login,
    logout,
    checkAuthStatus,
    loadUserProfile,
    refreshProfile,
  };

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};