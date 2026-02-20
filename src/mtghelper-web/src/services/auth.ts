// JWT token management service

const ACCESS_TOKEN_KEY = 'mtghelper_access_token';
const REFRESH_TOKEN_KEY = 'mtghelper_refresh_token';

/**
 * Store access and refresh tokens in localStorage
 */
export function setTokens(accessToken: string, refreshToken: string): void {
  localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
}

/**
 * Retrieve the access token from localStorage
 */
export function getToken(): string | null {
  return localStorage.getItem(ACCESS_TOKEN_KEY);
}

/**
 * Retrieve the refresh token from localStorage
 */
export function getRefreshToken(): string | null {
  return localStorage.getItem(REFRESH_TOKEN_KEY);
}

/**
 * Clear all tokens from localStorage
 */
export function clearTokens(): void {
  localStorage.removeItem(ACCESS_TOKEN_KEY);
  localStorage.removeItem(REFRESH_TOKEN_KEY);
}

/**
 * Decode a JWT token to extract the payload
 */
function decodeJWT(token: string): { exp?: number } | null {
  try {
    const parts = token.split('.');
    if (parts.length !== 3) return null;

    const payload = parts[1];
    const decoded = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(decoded);
  } catch {
    return null;
  }
}

/**
 * Check if the user is authenticated (token exists and is not expired)
 */
export function isAuthenticated(): boolean {
  const token = getToken();
  if (!token) return false;

  const decoded = decodeJWT(token);
  if (!decoded || !decoded.exp) return false;

  // Check if token is expired (exp is in seconds, Date.now() is in milliseconds)
  const isExpired = decoded.exp * 1000 < Date.now();
  return !isExpired;
}

/**
 * Get authorization headers for API requests
 */
export function getAuthHeaders(): Record<string, string> {
  const token = getToken();
  if (!token) return {};

  return {
    Authorization: `Bearer ${token}`
  };
}

/**
 * Refresh the access token using the stored refresh token
 * Returns true if successful, false otherwise
 */
export async function refreshAccessToken(): Promise<boolean> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return false;

  try {
    const response = await fetch('/api/auth/refresh', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ refreshToken }),
    });

    if (!response.ok) {
      clearTokens();
      return false;
    }

    const data = await response.json();
    if (data.token && data.refreshToken) {
      setTokens(data.token, data.refreshToken);
      return true;
    }

    return false;
  } catch (error) {
    console.error('Failed to refresh access token:', error);
    clearTokens();
    return false;
  }
}
