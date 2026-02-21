// =============================================================================
// main.tsx - Application Entry Point
// =============================================================================
// This is the entry point for the React application. It sets up the root
// React component tree with all necessary providers and renders it into
// the DOM element with id="root" in index.html.
//
// Providers configured here:
// - React.StrictMode: Enables additional checks and warnings in development
// - QueryClientProvider: Provides React Query for data fetching
// - BrowserRouter: Enables client-side routing with React Router
// =============================================================================

import React from 'react'
import ReactDOM from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import App from './App'
import { AuthProvider } from './contexts/AuthContext'
import './index.css'

// =============================================================================
// React Query Configuration
// =============================================================================

/**
 * Create a QueryClient instance for React Query.
 *
 * React Query (TanStack Query) is a data fetching library that provides:
 * - Automatic caching of API responses
 * - Background refetching to keep data fresh
 * - Loading and error state management
 * - Request deduplication (multiple components requesting same data)
 *
 * Configuration options:
 * - staleTime: How long data is considered "fresh" before refetching
 *   Set to 5 minutes to reduce unnecessary API calls
 * - retry: Number of retry attempts for failed requests
 *   Set to 1 to retry once before showing error
 */
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Data is considered fresh for 5 minutes
      // During this time, cached data is returned immediately
      // After this time, a background refetch occurs
      staleTime: 5 * 60 * 1000, // 5 minutes in milliseconds

      // Retry failed requests once before showing error
      // This handles temporary network issues gracefully
      retry: 1,
    },
  },
})

// =============================================================================
// Application Mount
// =============================================================================

/**
 * Mount the React application to the DOM.
 *
 * This creates the React root and renders the component tree:
 *
 * <StrictMode>                    - Development checks
 *   <QueryClientProvider>         - Data fetching context
 *     <BrowserRouter>             - Routing context
 *       <App />                   - Main application component
 *     </BrowserRouter>
 *   </QueryClientProvider>
 * </StrictMode>
 *
 * The providers must be nested in this order because:
 * - StrictMode wraps everything for development checks
 * - QueryClientProvider provides the query client to all children
 * - BrowserRouter provides routing context to App and its children
 */
ReactDOM.createRoot(
  // Get the root element from index.html
  // This is the <div id="root"></div> element
  document.getElementById('root')!
).render(
  // StrictMode enables additional development-time checks:
  // - Identifies components with unsafe lifecycles
  // - Warns about deprecated APIs
  // - Detects unexpected side effects
  // - Ensures reusable state (components mount/unmount twice in dev)
  <React.StrictMode>
    {/* QueryClientProvider makes the queryClient available to all child components */}
    {/* Any component can now use useQuery/useMutation hooks */}
    <QueryClientProvider client={queryClient}>
      {/* BrowserRouter enables client-side routing using the History API */}
      {/* This allows navigation without full page reloads */}
      <BrowserRouter>
        <AuthProvider>
          <App />
        </AuthProvider>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>,
)
