import { useMemo, useState, useRef, useEffect } from 'react'
import { Routes, Route, Link } from 'react-router-dom'
import { TAGLINES } from './taglines'
import SetList from './components/SetList'
import SetDetail from './components/SetDetail'
import ReserveListPage from './components/ReserveListPage'
import BoostersPage from './components/BoostersPage'
import SlabbedCardsPage from './components/SlabbedCardsPage'
import LoginPage from './components/LoginPage'
import ProfilePage from './components/ProfilePage'
import SettingsPage from './components/SettingsPage'
import AdminPage from './components/AdminPage'
import ProtectedRoute from './components/ProtectedRoute'
import AdminRoute from './components/AdminRoute'
import { useAuth } from './contexts/AuthContext'

function App() {
  const { user, logout } = useAuth()
  const tagline = useMemo(() => TAGLINES[Math.floor(Math.random() * TAGLINES.length)], [])
  const [dropdownOpen, setDropdownOpen] = useState(false)
  const dropdownRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    if (!dropdownOpen) return
    function handleClickOutside(e: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => document.removeEventListener('mousedown', handleClickOutside)
  }, [dropdownOpen])

  return (
    <div className="min-h-screen">
      <header className="bg-blue-800 text-white shadow-lg">
        <div className="container mx-auto px-4 py-4">
          <div className="flex items-center justify-between">
            <Link to="/" className="text-2xl font-bold hover:text-blue-200">
              CountOrSell
            </Link>

            <nav className="flex gap-4 items-center">
              <Link to="/" className="hover:text-blue-200">Sets</Link>
              {user && (
                <>
                  <Link to="/reservelist" className="hover:text-blue-200">Reserve List</Link>
                  <Link to="/boosters" className="hover:text-blue-200">Boosters</Link>
                  <Link to="/slabbed" className="hover:text-blue-200">Slabs</Link>
                </>
              )}
              {user ? (
                <div className="relative" ref={dropdownRef}>
                  <button
                    onClick={() => setDropdownOpen(o => !o)}
                    className="text-sm text-blue-200 hover:text-white flex items-center gap-1"
                  >
                    {user.displayName || user.username}
                    <span className="text-xs opacity-70">▾</span>
                  </button>
                  {dropdownOpen && (
                    <div className="absolute right-0 mt-2 bg-white text-gray-900 shadow-lg rounded py-1 min-w-[140px] z-50">
                      <Link
                        to="/profile"
                        onClick={() => setDropdownOpen(false)}
                        className="block px-4 py-2 text-sm hover:bg-gray-100"
                      >
                        Profile
                      </Link>
                      <Link
                        to="/settings"
                        onClick={() => setDropdownOpen(false)}
                        className="block px-4 py-2 text-sm hover:bg-gray-100"
                      >
                        Settings
                      </Link>
                      {user.isAdmin && (
                        <Link
                          to="/admin"
                          onClick={() => setDropdownOpen(false)}
                          className="block px-4 py-2 text-sm hover:bg-gray-100"
                        >
                          Admin
                        </Link>
                      )}
                      <button
                        onClick={() => { logout(); setDropdownOpen(false) }}
                        className="block w-full text-left px-4 py-2 text-sm hover:bg-gray-100"
                      >
                        Logout
                      </button>
                    </div>
                  )}
                </div>
              ) : (
                <Link
                  to="/login"
                  className="px-3 py-1 bg-blue-600 hover:bg-blue-500 rounded text-sm"
                >
                  Login
                </Link>
              )}
            </nav>
          </div>
        </div>
      </header>

      <main className="container mx-auto px-4 py-8">
        <Routes>
          <Route path="/" element={<SetList />} />
          <Route path="/sets/:setCode" element={<SetDetail />} />
          <Route path="/login" element={<LoginPage />} />
          <Route path="/reservelist" element={
            <ProtectedRoute><ReserveListPage /></ProtectedRoute>
          } />
          <Route path="/boosters" element={
            <ProtectedRoute><BoostersPage /></ProtectedRoute>
          } />
          <Route path="/slabbed" element={
            <ProtectedRoute><SlabbedCardsPage /></ProtectedRoute>
          } />
          <Route path="/profile" element={
            <ProtectedRoute><ProfilePage /></ProtectedRoute>
          } />
          <Route path="/settings" element={
            <ProtectedRoute><SettingsPage /></ProtectedRoute>
          } />
          <Route path="/admin" element={
            <AdminRoute><AdminPage /></AdminRoute>
          } />
        </Routes>
      </main>

      <footer className="bg-gray-800 text-gray-400 py-4 mt-8">
        <div className="container mx-auto px-4 text-center text-sm">
          <p className="mb-1">CountOrSell: {tagline}</p>
          <p>Data provided by{' '}
          <a
            href="https://scryfall.com"
            className="text-blue-400 hover:underline"
            target="_blank"
            rel="noopener noreferrer"
          >
            Scryfall
          </a></p>
        </div>
      </footer>
    </div>
  )
}

export default App
