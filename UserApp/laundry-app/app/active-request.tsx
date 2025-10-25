import { Stack, useFocusEffect, useRouter } from 'expo-router';
import { CheckCircle, Clock, MapPin, Package, Truck } from 'lucide-react-native';
import React, { useCallback, useEffect, useRef, useState } from 'react';
import {
        RefreshControl,
        ScrollView,
        StyleSheet,
        Text,
        TouchableOpacity,
        View,
} from 'react-native';
import { useCustomAlert } from '../components/CustomAlert';
import { ThemedText } from '../components/ThemedText';
import { ThemedView } from '../components/ThemedView';
import { useAuth } from '../contexts/AuthContext';
import { useThemeColor } from '../hooks/useThemeColor';
import { apiGet } from '../services/api';
import { laundryService } from '../services/laundryService';
import { notificationService } from '../services/notificationService';
import { formatRelativeTime } from '../utils/dateUtils';

interface ActiveRequest {
        id: number;
        status: string;
        weight?: number;
        totalCost?: number;
        requestedAt: string;
        scheduledAt: string;
        assignedRobot?: string;
        loadedAt?: string;
        completedAt?: string;
        robotStatus?: string;
        atUserRoom?: boolean;
}

export default function ActiveRequestScreen() {
        const { user } = useAuth();
        const router = useRouter();
        const [activeRequest, setActiveRequest] = useState<ActiveRequest | null>(null);
        const [isLoading, setIsLoading] = useState(false);
        const [isConfirming, setIsConfirming] = useState(false);
        const [timeRemaining, setTimeRemaining] = useState<number>(0);
        const [timerDurationSeconds, setTimerDurationSeconds] = useState<number>(300); // Default 5 minutes
        const [robotWeight, setRobotWeight] = useState<number>(0);
        const { showAlert, AlertComponent } = useCustomAlert();

        const backgroundColor = useThemeColor({}, 'background');
        const textColor = useThemeColor({}, 'text');
        const primaryColor = useThemeColor({}, 'primary');
        const secondaryColor = useThemeColor({}, 'secondary');
        const cardColor = useThemeColor({}, 'card');
        const borderColor = useThemeColor({}, 'border');
        const mutedColor = useThemeColor({}, 'muted');
        const warningColor = useThemeColor({}, 'warning');

        // Track previous status for change detection
        const previousStatusRef = useRef<string | null>(null);

        const loadActiveRequest = async () => {
                try {
                        setIsLoading(true);
                        const request = await laundryService.getActiveRequest();
                        setActiveRequest(request);

                        // Load robot weight data if robot is assigned and at room
                        if (request && request.assignedRobot && getStatusString(request.status) === 'arrivedatroom') {
                                await loadRobotWeight(request.assignedRobot);
                        }
                } catch (error: any) {
                        console.error('Error loading active request:', error);
                        showAlert('Error', 'Failed to load active request');
                } finally {
                        setIsLoading(false);
                }
        };

        const loadRobotWeight = async (robotName: string) => {
                try {
                        // Get robot weight from backend via request details
                        if (activeRequest) {
                                const response = await apiGet(`/requests/${activeRequest.id}/details`);
                                if (response?.data?.detectedWeightKg) {
                                        setRobotWeight(response.data.detectedWeightKg);
                                }
                        }
                } catch (error: any) {
                        console.error('Error loading robot weight:', error);
                        setRobotWeight(0);
                }
        };

        const handleConfirmLaundryLoaded = async () => {
                if (!activeRequest?.weight) {
                        showAlert('Error', 'No weight detected. Please try again.');
                        return;
                }

                try {
                        // Fetch current pricing from API
                        const response = await apiGet('/requests/pricing');
                        const pricing = response?.data;
                        
                        // Calculate cost based on weight
                        const pricePerKg = pricing.pricePerKg || 25.00;
                        const minimumCharge = pricing.minimumCharge || 50.00;
                        const totalCost = Math.max(activeRequest.weight * pricePerKg, minimumCharge);

                        // Show weight and cost approval dialog
                        showAlert(
                                'Approve Weight & Cost',
                                `Detected Weight: ${activeRequest.weight}kg\nTotal Cost: ₱${totalCost.toFixed(2)}\n\nDo you approve this weight and cost?`,
                        [
                                {
                                        text: 'Cancel',
                                        style: 'cancel'
                                },
                                {
                                        text: 'Approve',
                                        style: 'default',
                                        onPress: async () => {
                                                // User approved - proceed with confirmation
                                                setIsConfirming(true);
                                                try {
                                                        const result = await laundryService.confirmLaundryLoadedWithWeight(activeRequest!.id, activeRequest.weight);
                                                        showAlert('Success', result.message);
                                                        await loadActiveRequest(); // Refresh the request
                                                } catch (error: any) {
                                                        showAlert('Error', error.response?.data?.message || 'Failed to confirm laundry loading');
                                                } finally {
                                                        setIsConfirming(false);
                                                }
                                        }
                                }
                        ]
                );
                } catch (error) {
                        console.error('Error fetching pricing:', error);
                        showAlert('Error', 'Failed to fetch pricing information');
                }
        };

        const handleConfirmLaundryUnloaded = async () => {
                if (!activeRequest) return;

                setIsConfirming(true);
                try {
                        const result = await laundryService.confirmLaundryUnloaded(activeRequest.id);
                        showAlert('Success', result.message);
                        await loadActiveRequest(); // Refresh the request
                } catch (error: any) {
                        showAlert('Error', error.response?.data?.message || 'Failed to confirm laundry unloading');
                } finally {
                        setIsConfirming(false);
                }
        };

        const handleDeliveryChoice = async (deliveryType: 'Delivery' | 'Pickup') => {
                if (!activeRequest) return;

                const message = deliveryType === 'Delivery' 
                        ? 'Robot will deliver your clean laundry to your room. Please wait for the robot to arrive.'
                        : 'Your laundry is marked for pickup. You can collect it from the laundry room when convenient.';

                showAlert(
                        'Confirm Delivery Option',
                        `${message}\n\nProceed with ${deliveryType.toLowerCase()}?`,
                        [
                                {
                                        text: 'Cancel',
                                        style: 'cancel'
                                },
                                {
                                        text: 'Confirm',
                                        style: 'default',
                                        onPress: async () => {
                                                setIsConfirming(true);
                                                try {
                                                        const result = await laundryService.selectDeliveryOption(activeRequest.id, deliveryType);
                                                        showAlert('Success', result.message);
                                                        await loadActiveRequest(); // Refresh the request
                                                } catch (error: any) {
                                                        showAlert('Error', error.response?.data?.message || 'Failed to process delivery choice');
                                                } finally {
                                                        setIsConfirming(false);
                                                }
                                        }
                                }
                        ]
                );
        };

        // Fetch timer settings from backend
        const loadTimerSettings = async () => {
                try {
                        const response = await apiGet('/requests/timer-settings');
                        if (response?.data?.roomArrivalTimeoutSeconds) {
                                setTimerDurationSeconds(response.data.roomArrivalTimeoutSeconds);
                        }
                } catch (error: any) {
                        console.error('Error loading timer settings:', error);
                        // Keep default 300 seconds (5 minutes) if fetch fails
                }
        };

        useEffect(() => {
                loadTimerSettings(); // Load timer settings once on mount
                loadActiveRequest();

                // Auto-refresh every 5 seconds
                const interval = setInterval(loadActiveRequest, 5000);
                return () => clearInterval(interval);
        }, []);

        // Timer effect for ArrivedAtRoom status
        useEffect(() => {
                let timer: NodeJS.Timeout | null = null;

                if (activeRequest && (getStatusString(activeRequest.status) === 'arrivedatroom' || getStatusString(activeRequest.status) === 'finishedwashingarrivedatroom') && activeRequest.arrivedAtRoomAt) {
                        // Calculate time remaining using dynamic timer duration from server
                        // Parse UTC time properly and convert to local time for calculation
                        const arrivedTimeUTC = new Date(activeRequest.arrivedAtRoomAt + (activeRequest.arrivedAtRoomAt.endsWith('Z') ? '' : 'Z'));
                        const currentTimeUTC = new Date();
                        const elapsed = Math.floor((currentTimeUTC.getTime() - arrivedTimeUTC.getTime()) / 1000);
                        const remaining = Math.max(timerDurationSeconds - elapsed, 0);

                        console.log('Timer sync - ArrivedAt:', activeRequest.arrivedAtRoomAt, 'Duration:', timerDurationSeconds, 'Elapsed:', elapsed, 'Remaining:', remaining);
                        setTimeRemaining(remaining);

                        if (remaining > 0) {
                                timer = setInterval(() => {
                                        setTimeRemaining((prev) => {
                                                const newTime = Math.max(prev - 1, 0);
                                                if (newTime === 0) {
                                                        // Time's up - reload to get updated status
                                                        loadActiveRequest();
                                                }
                                                return newTime;
                                        });
                                }, 1000);
                        }
                } else if (activeRequest && getStatusString(activeRequest.status) !== 'arrivedatroom' && getStatusString(activeRequest.status) !== 'finishedwashingarrivedatroom') {
                        // Clear timer if status is not ArrivedAtRoom
                        setTimeRemaining(0);
                }

                return () => {
                        if (timer) clearInterval(timer);
                };
        }, [activeRequest, timerDurationSeconds]);

        // Status change detection for notifications
        useEffect(() => {
                if (!activeRequest) {
                        previousStatusRef.current = null;
                        return;
                }

                const currentStatus = getStatusString(activeRequest.status);
                const previousStatus = previousStatusRef.current;

                // Only send notification if status actually changed
                if (previousStatus && previousStatus !== currentStatus) {
                        console.log(`Status changed: ${previousStatus} -> ${currentStatus}`);
                        
                        // Send notification for the new status
                        notificationService.sendStatusNotification(
                                currentStatus,
                                activeRequest.id,
                                {
                                        weight: activeRequest.weight,
                                        totalCost: activeRequest.totalCost
                                }
                        );
                }

                // Update the ref to current status
                previousStatusRef.current = currentStatus;
        }, [activeRequest]);


        // Refresh data when screen comes into focus
        useFocusEffect(
                useCallback(() => {
                        loadActiveRequest();
                }, [])
        );

        const getStatusString = (status: any): string => {
                // Convert backend enum to string
                switch (Number(status)) {
                        case 0: return 'pending';
                        case 1: return 'accepted';
                        case 2: return 'inprogress';
                        case 3: return 'robotenroute';
                        case 4: return 'arrivedatroom';
                        case 5: return 'laundryloaded';
                        case 6: return 'returnedtobase';
                        case 7: return 'weighingcomplete';
                        case 8: return 'paymentpending';
                        case 9: return 'completed';
                        case 10: return 'declined';
                        case 11: return 'cancelled';
                        case 12: return 'washing';
                        case 13: return 'finishedwashingarrivedatroom';
                        case 14: return 'finishedwashinggoingtoroom';
                        case 15: return 'finishedwashing';
                        case 27: return 'finishedwashinggoingtobase';
                        case 28: return 'finishedwashingawaitingpickup';
                        case 29: return 'finishedwashingatbase';
                        default: return String(status).toLowerCase();
                }
        };

        const getStatusInfo = (status: any) => {
                const statusStr = getStatusString(status);
                switch (statusStr) {
                        case 'pending':
                                return {
                                        icon: Clock,
                                        color: warningColor,
                                        title: '⏳ Awaiting Approval',
                                        description: 'Request submitted, waiting for admin approval'
                                };
                        case 'accepted':
                                return {
                                        icon: CheckCircle,
                                        color: primaryColor,
                                        title: '✅ Approved',
                                        description: 'Request approved, robot will be dispatched soon'
                                };
                        case 'inprogress':
                                return {
                                        icon: CheckCircle,
                                        color: primaryColor,
                                        title: '🔄 Processing',
                                        description: 'Request is being processed'
                                };
                        case 'robotenroute':
                                return {
                                        icon: Truck,
                                        color: primaryColor,
                                        title: '🤖 Robot En Route',
                                        description: 'Robot is navigating to your room'
                                };
                        case 'arrivedatroom':
                                return {
                                        icon: MapPin,
                                        color: secondaryColor,
                                        title: '📍 Robot Arrived',
                                        description: 'Robot has arrived at your room for pickup'
                                };
                        case 'laundryloaded':
                                return {
                                        icon: Package,
                                        color: primaryColor,
                                        title: '📦 Laundry Loaded',
                                        description: 'Laundry loaded, robot returning to base'
                                };
                        case 'returnedtobase':
                                return {
                                        icon: CheckCircle,
                                        color: primaryColor,
                                        title: '🏠 Returned to Base',
                                        description: 'Robot has returned with your laundry'
                                };
                        case 'weighingcomplete':
                                return {
                                        icon: CheckCircle,
                                        color: primaryColor,
                                        title: '⚖️ Weighing Complete',
                                        description: 'Your laundry has been weighed'
                                };
                        case 'washing':
                                return {
                                        icon: Package,
                                        color: primaryColor,
                                        title: '🌊 Washing in Progress',
                                        description: 'Your laundry is being washed'
                                };
                        case 'finishedwashing':
                                return {
                                        icon: CheckCircle,
                                        color: secondaryColor,
                                        title: '✨ Ready for Pickup',
                                        description: 'Laundry is clean! Choose pickup or delivery'
                                };
                        case 'finishedwashinggoingtoroom':
                                return {
                                        icon: Truck,
                                        color: primaryColor,
                                        title: '🚚 Delivery in Progress',
                                        description: 'Robot is delivering your clean laundry'
                                };
                        case 'finishedwashingarrivedatroom':
                                return {
                                        icon: MapPin,
                                        color: secondaryColor,
                                        title: '📍 Delivery Arrived',
                                        description: 'Robot has arrived with your clean laundry'
                                };
                        case 'finishedwashinggoingtobase':
                                return {
                                        icon: Truck,
                                        color: primaryColor,
                                        title: '🏠 Returning to Base',
                                        description: 'Robot is returning after delivery'
                                };
                        case 'finishedwashingawaitingpickup':
                                return {
                                        icon: Package,
                                        color: secondaryColor,
                                        title: '📦 Ready for Pickup',
                                        description: 'Clean laundry is ready for pickup at the laundry area'
                                };
                        case 'finishedwashingatbase':
                                return {
                                        icon: CheckCircle,
                                        color: secondaryColor,
                                        title: '✅ Complete - Admin Finalizing',
                                        description: 'Service completed, admin is finalizing your request'
                                };
                        default:
                                return {
                                        icon: Clock,
                                        color: mutedColor,
                                        title: status,
                                        description: 'Status update'
                                };
                }
        };

        if (!activeRequest) {
                return (
                        <>
                                <Stack.Screen options={{ headerShown: false }} />
                                <ThemedView style={styles.container}>
                                        <ScrollView
                                                style={styles.scrollContainer}
                                                refreshControl={
                                                        <RefreshControl refreshing={isLoading} onRefresh={loadActiveRequest} />
                                                }
                                        >
                                                <View style={styles.emptyState}>
                                                        <Package size={64} color={mutedColor} />
                                                        <ThemedText style={[styles.emptyTitle, { color: mutedColor }]}>
                                                                No Active Request
                                                        </ThemedText>
                                                        <ThemedText style={[styles.emptySubtitle, { color: mutedColor }]}>
                                                                You don&apos;t have any active laundry requests
                                                        </ThemedText>
                                                        <TouchableOpacity
                                                                style={[styles.createButton, { backgroundColor: primaryColor }]}
                                                                onPress={() => router.push('/(tabs)/request')}
                                                        >
                                                                <Package size={20} color="#ffffff" />
                                                                <Text style={styles.createButtonText}>Create Request</Text>
                                                        </TouchableOpacity>
                                                </View>
                                        </ScrollView>
                                </ThemedView>
                        </>
                );
        }

        const statusInfo = getStatusInfo(activeRequest.status);
        const StatusIcon = statusInfo.icon;

        return (
                <>
                        <Stack.Screen options={{ headerShown: false }} />
                        <ThemedView style={styles.container}>
                                <ScrollView
                                        style={styles.scrollContainer}
                                        refreshControl={
                                                <RefreshControl refreshing={isLoading} onRefresh={loadActiveRequest} />
                                        }
                                >
                                        <View style={styles.header}>
                                                <StatusIcon size={48} color={statusInfo.color} />
                                                <ThemedText style={styles.title}>Request #{activeRequest.id}</ThemedText>
                                                <ThemedText style={[styles.subtitle, { color: mutedColor }]}>
                                                        {user?.customerName}
                                                </ThemedText>
                                        </View>

                                        <View style={[styles.statusCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                                                <View style={styles.statusHeader}>
                                                        <StatusIcon size={24} color={statusInfo.color} />
                                                        <View style={styles.statusText}>
                                                                <ThemedText style={styles.statusTitle}>{statusInfo.title}</ThemedText>
                                                                <ThemedText style={[styles.statusDescription, { color: mutedColor }]}>
                                                                        {statusInfo.description}
                                                                </ThemedText>
                                                        </View>
                                                </View>
                                        </View>

                                        <View style={[styles.detailsCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                                                <ThemedText style={styles.cardTitle}>Request Details</ThemedText>

                                                <View style={styles.detailRow}>
                                                        <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Requested:</ThemedText>
                                                        <ThemedText style={styles.detailValue}>
                                                                {formatRelativeTime(activeRequest.requestedAt)}
                                                        </ThemedText>
                                                </View>

                                                {activeRequest.assignedRobot && (
                                                        <View style={styles.detailRow}>
                                                                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Assigned Robot:</ThemedText>
                                                                <ThemedText style={styles.detailValue}>{activeRequest.assignedRobot}</ThemedText>
                                                        </View>
                                                )}

                                                {activeRequest.robotStatus && (
                                                        <View style={styles.detailRow}>
                                                                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Robot Status:</ThemedText>
                                                                <ThemedText style={styles.detailValue}>{activeRequest.robotStatus}</ThemedText>
                                                        </View>
                                                )}

                                                {activeRequest.atUserRoom && (
                                                        <View style={[styles.actionSection, { borderTopColor: borderColor }]}>
                                                                <ThemedText style={[styles.actionTitle, { color: textColor }]}>Robot at Your Room</ThemedText>
                                                                <ThemedText style={[styles.actionDescription, { color: mutedColor }]}>
                                                                        The robot has arrived! Please load your laundry and tap the button below.
                                                                </ThemedText>
                                                                <TouchableOpacity
                                                                        style={[styles.actionButton, { backgroundColor: primaryColor }]}
                                                                        onPress={handleConfirmLaundryLoaded}
                                                                        disabled={isConfirming}
                                                                >
                                                                        <ThemedText style={styles.actionButtonText}>
                                                                                {isConfirming ? 'Processing...' : 'I have loaded the laundry'}
                                                                        </ThemedText>
                                                                </TouchableOpacity>
                                                        </View>
                                                )}

                                                {activeRequest.weight && (
                                                        <View style={styles.detailRow}>
                                                                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Weight:</ThemedText>
                                                                <ThemedText style={styles.detailValue}>{activeRequest.weight}kg</ThemedText>
                                                        </View>
                                                )}

                                                {activeRequest.totalCost && (
                                                        <View style={styles.detailRow}>
                                                                <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Total Cost:</ThemedText>
                                                                <ThemedText style={[styles.detailValue, { color: secondaryColor }]}>
                                                                        ₱{activeRequest.totalCost}
                                                                </ThemedText>
                                                        </View>
                                                )}
                                        </View>

                                        {getStatusString(activeRequest.status) === 'arrivedatroom' && (
                                                <View style={[styles.actionCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                                                        <ThemedText style={styles.actionTitle}>Ready to Load Laundry?</ThemedText>
                                                        <ThemedText style={[styles.actionDescription, { color: mutedColor }]}>
                                                                The robot has arrived at your room. Load your laundry and confirm when done.
                                                        </ThemedText>

                                                        {/* Timer Display */}
                                                        <View style={[styles.timerCard, { backgroundColor: warningColor + '20', borderColor: warningColor }]}>
                                                                <View style={styles.timerHeader}>
                                                                        <Clock size={20} color={warningColor} />
                                                                        <ThemedText style={[styles.timerTitle, { color: warningColor }]}>
                                                                                Time Remaining: {Math.floor(timeRemaining / 60)}:{(timeRemaining % 60).toString().padStart(2, '0')}
                                                                        </ThemedText>
                                                                </View>
                                                                <ThemedText style={[styles.timerDescription, { color: mutedColor }]}>
                                                                        Please load your laundry within the time limit
                                                                </ThemedText>
                                                        </View>

                                                        {/* Weight Detection Display */}
                                                        {robotWeight > 0 && (
                                                                <View style={[styles.weightCard, { backgroundColor: primaryColor + '20', borderColor: primaryColor }]}>
                                                                        <View style={styles.weightHeader}>
                                                                                <Package size={20} color={primaryColor} />
                                                                                <ThemedText style={[styles.weightTitle, { color: primaryColor }]}>
                                                                                        Robot Weight Detection: {robotWeight.toFixed(3)}kg
                                                                                </ThemedText>
                                                                        </View>
                                                                </View>
                                                        )}

                                                        <TouchableOpacity
                                                                style={[styles.confirmButton, { backgroundColor: isConfirming ? mutedColor : secondaryColor }]}
                                                                onPress={handleConfirmLaundryLoaded}
                                                                disabled={isConfirming}
                                                        >
                                                                <CheckCircle size={20} color="#ffffff" />
                                                                <Text style={styles.confirmButtonText}>
                                                                        {isConfirming ? 'Confirming...' : 'Confirm Laundry Loaded'}
                                                                </Text>
                                                        </TouchableOpacity>
                                                </View>
                                        )}

                                        {getStatusString(activeRequest.status) === 'finishedwashingarrivedatroom' && (
                                                <View style={[styles.actionCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                                                        <ThemedText style={styles.actionTitle}>Delivery Arrived!</ThemedText>
                                                        <ThemedText style={[styles.actionDescription, { color: mutedColor }]}>
                                                                The robot has arrived with your clean laundry. Please unload your laundry and confirm when done.
                                                        </ThemedText>

                                                        {/* Timer Display for delivery */}
                                                        <View style={[styles.timerCard, { backgroundColor: warningColor + '20', borderColor: warningColor }]}>
                                                                <View style={styles.timerHeader}>
                                                                        <Clock size={20} color={warningColor} />
                                                                        <ThemedText style={[styles.timerTitle, { color: warningColor }]}>
                                                                                Time Remaining: {Math.floor(timeRemaining / 60)}:{(timeRemaining % 60).toString().padStart(2, '0')}
                                                                        </ThemedText>
                                                                </View>
                                                                <ThemedText style={[styles.timerDescription, { color: mutedColor }]}>
                                                                        Please unload your clean laundry within the time limit
                                                                </ThemedText>
                                                        </View>

                                                        <TouchableOpacity
                                                                style={[styles.confirmButton, { backgroundColor: isConfirming ? mutedColor : secondaryColor }]}
                                                                onPress={handleConfirmLaundryUnloaded}
                                                                disabled={isConfirming}
                                                        >
                                                                <CheckCircle size={20} color="#ffffff" />
                                                                <Text style={styles.confirmButtonText}>
                                                                        {isConfirming ? 'Confirming...' : 'Confirm Laundry Unloaded'}
                                                                </Text>
                                                        </TouchableOpacity>
                                                </View>
                                        )}

                                        {getStatusString(activeRequest.status) === 'finishedwashing' && (
                                                <View style={[styles.actionCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
                                                        <ThemedText style={styles.actionTitle}>Choose Delivery Option</ThemedText>
                                                        <ThemedText style={[styles.actionDescription, { color: mutedColor }]}>
                                                                Your laundry is clean and ready! How would you like to receive it?
                                                        </ThemedText>

                                                        <View style={styles.deliveryOptions}>
                                                                <TouchableOpacity
                                                                        style={[styles.deliveryOption, { backgroundColor: primaryColor }]}
                                                                        onPress={() => handleDeliveryChoice('Delivery')}
                                                                        disabled={isConfirming}
                                                                >
                                                                        <Truck size={20} color="#ffffff" />
                                                                        <Text style={styles.deliveryOptionText}>
                                                                                {isConfirming ? 'Processing...' : 'Deliver to my room'}
                                                                        </Text>
                                                                </TouchableOpacity>

                                                                <TouchableOpacity
                                                                        style={[styles.deliveryOption, { backgroundColor: secondaryColor }]}
                                                                        onPress={() => handleDeliveryChoice('Pickup')}
                                                                        disabled={isConfirming}
                                                                >
                                                                        <Package size={20} color="#ffffff" />
                                                                        <Text style={styles.deliveryOptionText}>
                                                                                {isConfirming ? 'Processing...' : 'I will pick up'}
                                                                        </Text>
                                                                </TouchableOpacity>
                                                        </View>
                                                </View>
                                        )}

                                        {(getStatusString(activeRequest.status) === 'returnedtobase' || getStatusString(activeRequest.status) === 'finishedwashingatbase') && (
                                                <View style={[styles.actionCard, { backgroundColor: warningColor + '20', borderColor: warningColor }]}>
                                                        <View style={styles.actionHeader}>
                                                                <Clock size={24} color={warningColor} />
                                                                <ThemedText style={[styles.actionTitle, { color: warningColor, marginLeft: 12, marginBottom: 0 }]}>Awaiting Admin Completion</ThemedText>
                                                        </View>
                                                        <ThemedText style={[styles.actionDescription, { color: mutedColor }]}>
                                                                {getStatusString(activeRequest.status) === 'finishedwashingatbase' 
                                                                        ? 'Your clean laundry service is complete! Please wait while an administrator finalizes your request.'
                                                                        : 'Your laundry has been returned to base. Please wait while an administrator processes your request and confirms completion.'}
                                                        </ThemedText>
                                                </View>
                                        )}

                                        <TouchableOpacity
                                                style={[styles.backButton, { backgroundColor: cardColor, borderColor: borderColor }]}
                                                onPress={() => router.push('/(tabs)')}
                                        >
                                                <ThemedText style={[styles.backButtonText, { color: textColor }]}>
                                                        Back to Home
                                                </ThemedText>
                                        </TouchableOpacity>
                                </ScrollView>
                                <AlertComponent />
                        </ThemedView>
                </>
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
                paddingTop: 80,
        },
        title: {
                fontSize: 28,
                fontWeight: 'bold',
                marginTop: 16,
                marginBottom: 4,
        },
        subtitle: {
                fontSize: 16,
        },
        statusCard: {
                margin: 16,
                padding: 20,
                borderRadius: 16,
                borderWidth: 1,
        },
        statusHeader: {
                flexDirection: 'row',
                alignItems: 'center',
        },
        statusText: {
                flex: 1,
                marginLeft: 16,
        },
        statusTitle: {
                fontSize: 18,
                fontWeight: '600',
                marginBottom: 4,
        },
        statusDescription: {
                fontSize: 14,
        },
        detailsCard: {
                margin: 16,
                marginTop: 0,
                padding: 20,
                borderRadius: 16,
                borderWidth: 1,
        },
        cardTitle: {
                fontSize: 18,
                fontWeight: '600',
                marginBottom: 16,
        },
        detailRow: {
                flexDirection: 'row',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: 12,
        },
        detailLabel: {
                fontSize: 14,
        },
        detailValue: {
                fontSize: 14,
                fontWeight: '500',
        },
        actionCard: {
                margin: 16,
                marginTop: 0,
                padding: 20,
                borderRadius: 16,
                borderWidth: 1,
        },
        actionHeader: {
                flexDirection: 'row',
                alignItems: 'center',
                marginBottom: 12,
        },
        actionTitle: {
                fontSize: 18,
                fontWeight: '600',
                marginBottom: 8,
        },
        actionDescription: {
                fontSize: 14,
                marginBottom: 16,
                lineHeight: 20,
        },
        confirmButton: {
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 16,
                borderRadius: 12,
                gap: 8,
        },
        confirmButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        timerCard: {
                padding: 16,
                borderRadius: 12,
                borderWidth: 1,
                marginBottom: 16,
        },
        timerHeader: {
                flexDirection: 'row',
                alignItems: 'center',
                marginBottom: 8,
        },
        timerTitle: {
                fontSize: 16,
                fontWeight: '600',
                marginLeft: 8,
        },
        timerDescription: {
                fontSize: 14,
        },
        weightCard: {
                padding: 16,
                borderRadius: 12,
                borderWidth: 1,
                marginBottom: 16,
        },
        weightHeader: {
                flexDirection: 'row',
                alignItems: 'center',
        },
        weightTitle: {
                fontSize: 16,
                fontWeight: '600',
                marginLeft: 8,
        },
        backButton: {
                margin: 16,
                marginTop: 0,
                padding: 16,
                borderRadius: 12,
                borderWidth: 1,
                alignItems: 'center',
        },
        backButtonText: {
                fontSize: 16,
                fontWeight: '600',
        },
        emptyState: {
                alignItems: 'center',
                justifyContent: 'center',
                padding: 48,
                marginTop: 100,
        },
        emptyTitle: {
                fontSize: 20,
                fontWeight: '600',
                marginTop: 16,
                marginBottom: 8,
        },
        emptySubtitle: {
                fontSize: 14,
                textAlign: 'center',
                marginBottom: 24,
                lineHeight: 20,
        },
        createButton: {
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 16,
                borderRadius: 12,
                gap: 8,
        },
        createButtonText: {
                color: '#ffffff',
                fontSize: 16,
                fontWeight: '600',
        },
        actionSection: {
                marginTop: 20,
                paddingTop: 20,
                borderTopWidth: 1,
        },
        actionButton: {
                paddingVertical: 16,
                paddingHorizontal: 24,
                borderRadius: 12,
                alignItems: 'center',
        },
        actionButtonText: {
                color: '#FFFFFF',
                fontSize: 16,
                fontWeight: '600',
        },
        deliveryOptions: {
                flexDirection: 'row',
                gap: 12,
        },
        deliveryOption: {
                flex: 1,
                flexDirection: 'row',
                alignItems: 'center',
                justifyContent: 'center',
                padding: 16,
                borderRadius: 12,
                backgroundColor: '#3b82f6',
                gap: 8,
        },
        deliveryOptionText: {
                color: '#ffffff',
                fontSize: 14,
                fontWeight: '600',
        },
});
