/**
 * Below are the colors that are used in the app. The colors are defined in the light and dark mode.
 * There are many other ways to style your app. For example, [Nativewind](https://www.nativewind.dev/), [Tamagui](https://tamagui.dev/), [unistyles](https://reactnativeunistyles.vercel.app), etc.
 */

const tintColorLight = '#46C1FE';
const tintColorDark = '#46C1FE';

export const Colors = {
  light: {
    text: '#11181C',
    background: '#fff',
    tint: tintColorLight,
    icon: '#687076',
    tabIconDefault: '#687076',
    tabIconSelected: tintColorLight,
    primary: '#46C1FE',
    secondary: '#44D68C',
    danger: '#FF6270',
    warning: '#FEDD69',
    success: '#44D68C',
    muted: '#6b7280',
    textMuted: '#6b7280',
    card: '#f8fafc',
    border: '#e2e8f0',
  },
  dark: {
    text: '#ECEDEE',
    background: '#0F0E13',
    tint: tintColorDark,
    icon: '#9BA1A6',
    tabIconDefault: '#9BA1A6',
    tabIconSelected: tintColorDark,
    primary: '#46C1FE',
    secondary: '#44D68C',
    danger: '#FF6270',
    warning: '#FEDD69',
    success: '#44D68C',
    muted: '#9ca3af',
    textMuted: '#9ca3af',
    card: '#1a1923',
    sidebar: '#151419',
    border: '#2a2d3a',
  },
};
