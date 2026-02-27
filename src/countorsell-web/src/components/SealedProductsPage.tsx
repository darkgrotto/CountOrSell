import { useQuery } from '@tanstack/react-query'
import { api } from '../services/api'

interface SphProduct {
  id?: number
  name?: string
  productType?: string
  setCode?: string
  setName?: string
  msrp?: number | null
  imageUrl?: string | null
}

const TYPE_LABELS: Record<string, string> = {
  BoosterPack: 'Booster Pack',
  BoosterBox: 'Booster Box',
  PreconDeck: 'Precon Deck',
}

const TYPE_COLORS: Record<string, string> = {
  BoosterPack: 'bg-blue-900 text-blue-300',
  BoosterBox: 'bg-green-900 text-green-300',
  PreconDeck: 'bg-purple-900 text-purple-300',
}

interface Props {
  sphEnabled: boolean
}

export default function SealedProductsPage({ sphEnabled }: Props) {
  if (sphEnabled) {
    return <SphEmbedded />
  }
  return <SphStaticCatalog />
}

function SphEmbedded() {
  return (
    <div className="w-full" style={{ height: 'calc(100vh - 160px)' }}>
      <iframe
        src="/sph/"
        className="w-full h-full border-0 rounded-lg"
        title="Sealed Product Helper"
        allow="same-origin"
      />
    </div>
  )
}

function SphStaticCatalog() {
  const { data: products = [], isLoading, isError } = useQuery({
    queryKey: ['sph-products'],
    queryFn: () => api.getSphProducts() as Promise<SphProduct[]>,
  })

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20 text-gray-400">
        Loading sealed product catalog…
      </div>
    )
  }

  if (isError) {
    return (
      <div className="flex items-center justify-center py-20 text-red-400">
        Failed to load catalog. An admin must apply an SPH update first.
      </div>
    )
  }

  if (products.length === 0) {
    return (
      <div className="text-center py-20 text-gray-400">
        <p className="text-lg mb-2">No sealed products in catalog.</p>
        <p className="text-sm">An admin can apply an SPH catalog update from the Admin panel.</p>
      </div>
    )
  }

  return (
    <div>
      <div className="mb-6">
        <h1 className="text-2xl font-bold text-gray-100 mb-1">Sealed Products</h1>
        <p className="text-sm text-gray-400">{products.length} products — read-only catalog</p>
      </div>

      <div className="grid gap-4" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))' }}>
        {products.map((p, i) => (
          <div key={p.id ?? i} className="bg-gray-800 border border-gray-700 rounded-lg overflow-hidden">
            {p.imageUrl ? (
              <img
                src={p.imageUrl}
                alt={p.name ?? 'Product'}
                className="w-full object-contain bg-gray-900"
                style={{ aspectRatio: '3/2' }}
                loading="lazy"
              />
            ) : (
              <div
                className="w-full flex items-center justify-center bg-gray-900 text-4xl text-gray-700"
                style={{ aspectRatio: '3/2' }}
              >
                📦
              </div>
            )}
            <div className="p-3">
              <div className="font-medium text-gray-100 text-sm leading-snug mb-2">
                {p.name ?? 'Unknown Product'}
              </div>
              <div className="flex items-center justify-between gap-2">
                <span className="text-xs text-gray-500 uppercase tracking-wide font-medium">
                  {p.setCode ?? ''}
                </span>
                {p.productType && (
                  <span className={`text-xs px-2 py-0.5 rounded-full font-semibold ${TYPE_COLORS[p.productType] ?? 'bg-gray-700 text-gray-300'}`}>
                    {TYPE_LABELS[p.productType] ?? p.productType}
                  </span>
                )}
              </div>
              {p.msrp != null && (
                <div className="mt-1.5 text-sm font-semibold text-yellow-400">
                  ${Number(p.msrp).toFixed(2)}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
