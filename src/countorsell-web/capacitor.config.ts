import { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.countorsell.app',
  appName: 'CountOrSell',
  webDir: 'dist',
  server: {
    androidScheme: 'https'
  }
};

export default config;
