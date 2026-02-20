import { Routes, Route, Link } from 'react-router-dom'
import SetList from './components/SetList'
import SetDetail from './components/SetDetail'
import ReserveListPage from './components/ReserveListPage'
import BoostersPage from './components/BoostersPage'
import LoginPage from './components/LoginPage'
import ProtectedRoute from './components/ProtectedRoute'
import UpdateCheckPanel from './components/UpdateCheckPanel'
import { useAuth } from './contexts/AuthContext'

function App() {
  const { user, logout } = useAuth()

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
                </>
              )}
              <UpdateCheckPanel />
              {user ? (
                <div className="flex items-center gap-3">
                  <span className="text-sm text-blue-200">{user.displayName || user.username}</span>
                  <button
                    onClick={logout}
                    className="px-3 py-1 bg-blue-600 hover:bg-blue-500 rounded text-sm"
                  >
                    Logout
                  </button>
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
        </Routes>
      </main>

      <footer className="bg-gray-800 text-gray-400 py-4 mt-8">
        <div className="container mx-auto px-4 text-center text-sm">
          <p className="mb-1">CountOrSell — Your collection was getting out of hand. We fixed that.</p>
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
