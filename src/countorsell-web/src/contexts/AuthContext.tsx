import { createContext, useContext, useState, useEffect, ReactNode } from 'react';
import { setTokens, clearTokens, isAuthenticated as checkIsAuthenticated } from '../services/auth';
import { api } from '../services/api';

export interface UserInfo {
  id: string;
  username: string;
  displayName?: string;
}

interface AuthContextType {
  user: UserInfo | null;
  isLoading: boolean;
  login: (username: string, password: string) => Promise<void>;
  register: (username: string, password: string, displayName?: string) => Promise<void>;
  logout: () => void;
  updateProfile: (displayName: string) => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

interface AuthProviderProps {
  children: ReactNode;
}

export function AuthProvider({ children }: AuthProviderProps) {
  const [user, setUser] = useState<UserInfo | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  // On mount, check if user is authenticated and fetch user info
  useEffect(() => {
    async function loadUser() {
      if (checkIsAuthenticated()) {
        try {
          const response = await fetch('/api/auth/me', {
            headers: {
              Authorization: `Bearer ${localStorage.getItem('CountOrSell_access_token')}`,
            },
          });

          if (response.ok) {
            const userData = await response.json();
            setUser(userData);
          } else {
            clearTokens();
          }
        } catch (error) {
          console.error('Failed to fetch user info:', error);
          clearTokens();
        }
      }
      setIsLoading(false);
    }

    loadUser();
  }, []);

  const login = async (username: string, password: string) => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ username, password }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Login failed');
      }

      const data = await response.json();

      if (!data.token || !data.refreshToken) {
        throw new Error('Invalid response from server');
      }

      setTokens(data.token, data.refreshToken);

      // Fetch user info
      const userResponse = await fetch('/api/auth/me', {
        headers: {
          Authorization: `Bearer ${data.token}`,
        },
      });

      if (userResponse.ok) {
        const userData = await userResponse.json();
        setUser(userData);
      } else {
        throw new Error('Failed to fetch user info');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (username: string, password: string, displayName?: string) => {
    setIsLoading(true);
    try {
      const response = await fetch('/api/auth/register', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ username, password, displayName }),
      });

      if (!response.ok) {
        const error = await response.json();
        throw new Error(error.error || 'Registration failed');
      }

      const data = await response.json();

      if (!data.token || !data.refreshToken) {
        throw new Error('Invalid response from server');
      }

      setTokens(data.token, data.refreshToken);

      // Fetch user info
      const userResponse = await fetch('/api/auth/me', {
        headers: {
          Authorization: `Bearer ${data.token}`,
        },
      });

      if (userResponse.ok) {
        const userData = await userResponse.json();
        setUser(userData);
      } else {
        throw new Error('Failed to fetch user info');
      }
    } finally {
      setIsLoading(false);
    }
  };

  const logout = () => {
    clearTokens();
    setUser(null);
  };

  const updateProfile = async (displayName: string) => {
    const updated = await api.updateDisplayName(displayName);
    setUser(updated);
  };

  return (
    <AuthContext.Provider value={{ user, isLoading, login, register, logout, updateProfile }}>
      {children}
    </AuthContext.Provider>
  );
}
