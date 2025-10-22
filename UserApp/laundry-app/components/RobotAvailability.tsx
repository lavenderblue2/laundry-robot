import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ActivityIndicator } from 'react-native';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedText } from './ThemedText';
import { laundryService } from '../services/laundryService';

interface RobotAvailabilityProps {
  compact?: boolean;
}

export function RobotAvailability({ compact = false }: RobotAvailabilityProps) {
  const [robotData, setRobotData] = useState<{
    totalRobots: number;
    availableRobots: number;
    busyRobots: number;
    offlineRobots: number;
  } | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const cardColor = useThemeColor({}, 'card');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');
  const successColor = '#10B981'; // Green
  const warningColor = '#F59E0B'; // Orange
  const dangerColor = '#EF4444'; // Red

  const loadRobotAvailability = async () => {
    try {
      setIsLoading(true);
      const data = await laundryService.getAvailableRobots();
      setRobotData(data);
    } catch (error) {
      console.error('Error loading robot availability:', error);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    loadRobotAvailability();
    // Refresh every 30 seconds
    const interval = setInterval(loadRobotAvailability, 30000);
    return () => clearInterval(interval);
  }, []);

  if (isLoading) {
    return (
      <View style={[styles.container, { backgroundColor: cardColor, borderColor: borderColor }]}>
        <ActivityIndicator size="small" color={primaryColor} />
      </View>
    );
  }

  if (!robotData) {
    return null;
  }

  const getStatusColor = () => {
    if (robotData.availableRobots > 0) return successColor;
    if (robotData.busyRobots > 0) return warningColor;
    return dangerColor;
  };

  if (compact) {
    return (
      <View style={[styles.compactContainer, { backgroundColor: cardColor, borderColor: borderColor }]}>
        <View style={styles.compactContent}>
          <View style={[styles.statusDot, { backgroundColor: getStatusColor() }]} />
          <ThemedText style={styles.compactText}>
            {robotData.availableRobots > 0
              ? `${robotData.availableRobots} Robot${robotData.availableRobots !== 1 ? 's' : ''} Available`
              : robotData.busyRobots > 0
              ? 'All Robots Busy'
              : 'No Robots Available'}
          </ThemedText>
        </View>
      </View>
    );
  }

  return (
    <View style={[styles.container, { backgroundColor: cardColor, borderColor: borderColor }]}>
      <View style={styles.header}>
        <ThemedText style={styles.title}>🤖 Robot Fleet Status</ThemedText>
        <ThemedText style={[styles.subtitle, { color: mutedColor }]}>Live availability</ThemedText>
      </View>

      <View style={styles.statsGrid}>
        {/* Available Robots */}
        <View style={[styles.statCard, { backgroundColor: successColor + '15', borderColor: successColor + '30' }]}>
          <Text style={[styles.statNumber, { color: successColor }]}>{robotData.availableRobots}</Text>
          <ThemedText style={[styles.statLabel, { color: mutedColor }]}>Available</ThemedText>
        </View>

        {/* Busy Robots */}
        <View style={[styles.statCard, { backgroundColor: warningColor + '15', borderColor: warningColor + '30' }]}>
          <Text style={[styles.statNumber, { color: warningColor }]}>{robotData.busyRobots}</Text>
          <ThemedText style={[styles.statLabel, { color: mutedColor }]}>Busy</ThemedText>
        </View>

        {/* Total Active Robots */}
        <View style={[styles.statCard, { backgroundColor: primaryColor + '15', borderColor: primaryColor + '30' }]}>
          <Text style={[styles.statNumber, { color: primaryColor }]}>{robotData.totalRobots}</Text>
          <ThemedText style={[styles.statLabel, { color: mutedColor }]}>Online</ThemedText>
        </View>
      </View>

      {/* Status Message */}
      <View style={[styles.statusBanner, { backgroundColor: getStatusColor() + '20', borderColor: getStatusColor() + '40' }]}>
        <View style={[styles.statusDot, { backgroundColor: getStatusColor() }]} />
        <ThemedText style={[styles.statusText, { color: getStatusColor() }]}>
          {robotData.availableRobots > 0
            ? `${robotData.availableRobots} robot${robotData.availableRobots !== 1 ? 's' : ''} ready to serve you!`
            : robotData.busyRobots > 0
            ? 'All robots are currently busy. Your request will be queued.'
            : 'No robots available at the moment. Please try again later.'}
        </ThemedText>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: 16,
    padding: 20,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  compactContainer: {
    borderRadius: 12,
    padding: 12,
    borderWidth: 1,
  },
  compactContent: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
  },
  compactText: {
    fontSize: 14,
    fontWeight: '600',
  },
  header: {
    marginBottom: 16,
  },
  title: {
    fontSize: 18,
    fontWeight: '600',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 13,
  },
  statsGrid: {
    flexDirection: 'row',
    gap: 12,
    marginBottom: 16,
  },
  statCard: {
    flex: 1,
    borderRadius: 12,
    padding: 16,
    alignItems: 'center',
    borderWidth: 1,
  },
  statNumber: {
    fontSize: 32,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  statLabel: {
    fontSize: 12,
    fontWeight: '500',
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  statusBanner: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    borderRadius: 10,
    gap: 10,
    borderWidth: 1,
  },
  statusDot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  statusText: {
    flex: 1,
    fontSize: 13,
    fontWeight: '600',
    lineHeight: 18,
  },
});
