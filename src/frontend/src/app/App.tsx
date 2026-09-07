import { useMemo, useState, type ReactNode } from 'react'
import { BrowserRouter, Link, NavLink, Navigate, Route, Routes, useParams, useSearchParams } from 'react-router-dom'
import { EnterRankingDialog } from '../features/creator-entry/ui/EnterRankingDialog'
import { rankCreators } from '../features/leaderboard/application/rankCreators'
import { dummyCreators } from '../features/leaderboard/data/dummyCreators'
import { creatorCategories, creatorCategoryLabels, type Creator, type CreatorCategory, type RankedCreator } from '../features/leaderboard/domain/creator'
import { RankingList } from '../features/leaderboard/ui/RankingList'
import { BoostDialog } from '../features/payments/ui/BoostDialog'
import { appServices } from '../shared/config/appServices'
import { formatCurrency } from '../shared/format/currency'

function Layout({ children, onEnter }: { children: ReactNode; onEnter: () => void }) {
  const [menuOpen, setMenuOpen] = useState(false)
  return <><header className="site-header"><nav className="nav"><Link className="brand" to="/"><span>♛</span>CrownRank</Link><button className="menu-toggle" onClick={() => setMenuOpen(!menuOpen)} aria-label="Toggle navigation">☰</button><div className={`nav-actions ${menuOpen ? 'open' : ''}`}><NavLink to="/">Home</NavLink><NavLink to="/rankings">Global rank</NavLink><NavLink to="/daily">Daily rank</NavLink><NavLink to="/categories">Categories</NavLink><button className="primary-button compact" onClick={onEnter}>Enter ranking</button></div></nav></header><main>{children}</main><footer><Link className="brand" to="/"><span>♛</span>CrownRank</Link><p>Where creators rise on community support.</p><span>© 2026 CrownRank</span></footer></>
}

function CategoryFilter({ value, onChange }: { value: CreatorCategory | 'all'; onChange: (value: CreatorCategory | 'all') => void }) {
  return <label className="filter-select"><span>Category</span><select value={value} onChange={(e) => onChange(e.target.value as CreatorCategory | 'all')}><option value="all">All creators</option>{creatorCategories.map(category => <option value={category} key={category}>{creatorCategoryLabels[category]}</option>)}</select></label>
}

function BoardHeader({ eyebrow, title, copy, control }: { eyebrow: string; title: string; copy: string; control?: ReactNode }) {
  return <div className="board-heading"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{copy}</p></div>{control}</div>
}

function Home({ ranked, onBoost, onEnter }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void; onEnter: () => void }) {
  const [limit, setLimit] = useState(5)
  return <>
    <section className="hero"><div className="hero-copy"><p className="eyebrow">The fan-powered creator leaderboard</p><h1>Rise through the ranks.<br /><span>Claim the crown.</span></h1><p className="intro">Creators enter with any amount. Communities boost who they believe in. Every score is visible.</p><div className="hero-actions"><button className="primary-button large" onClick={onEnter}>Enter the ranking <span>→</span></button><Link to="/rankings">View global rank</Link></div><div className="trust-row"><span>No account required</span><span>Any starting amount</span><span>Transparent ranking</span></div></div><div className="crown-card"><span className="mini-label">Current world #1</span><img src={ranked[0].imageUrl} alt="" /><strong>{ranked[0].displayName}</strong><small>@{ranked[0].username}</small><b>{formatCurrency(ranked[0].totalContributedCents)}</b></div></section>
    <section className="board-section"><div className="section-heading"><div><p className="eyebrow">Global ranking</p><h2>Top creators worldwide</h2></div><Link to="/rankings">Full ranking →</Link></div><RankingList creators={ranked.slice(0, limit)} onBoost={onBoost} compact />{limit === 5 ? <button className="secondary-button board-more" onClick={() => setLimit(10)}>See top 10</button> : <Link className="secondary-button board-more" to="/rankings">See full ranking</Link>}</section>
    <section className="daily-preview"><div><p className="eyebrow">Today’s leaders</p><h2>Who is rising today?</h2><p>Daily scores reset at midnight UTC, giving every creator a fresh chance to lead.</p><Link className="text-link" to="/daily">See daily ranking →</Link></div><RankingList creators={ranked.slice().sort((a,b) => b.dailyContributedCents-a.dailyContributedCents).slice(0,3).map((c,i) => ({...c, rank:i+1, totalContributedCents:c.dailyContributedCents}))} onBoost={onBoost} compact /></section>
  </>
}

function Rankings({ ranked, onBoost }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void }) {
  const [params, setParams] = useSearchParams()
  const category = (params.get('category') ?? 'all') as CreatorCategory | 'all'
  const page = Math.max(1, Number(params.get('page') ?? 1))
  const filtered = category === 'all' ? ranked : ranked.filter(c => c.category === category)
  const pageSize = 25, pages = Math.max(1, Math.ceil(filtered.length / pageSize))
  const visible = filtered.slice((page - 1) * pageSize, page * pageSize)
  return <section className="page-shell"><BoardHeader eyebrow="All-time leaderboard" title="Global ranking" copy="The complete CrownRank board, ordered by total confirmed contributions." control={<CategoryFilter value={category} onChange={(next) => setParams(next === 'all' ? {} : { category: next })} />} /><RankingList creators={visible} onBoost={onBoost} /><nav className="pagination" aria-label="Ranking pages"><button disabled={page <= 1} onClick={() => setParams({ ...(category !== 'all' && { category }), page: String(page - 1) })}>← Previous</button><span>Page {Math.min(page,pages)} of {pages}</span><button disabled={page >= pages} onClick={() => setParams({ ...(category !== 'all' && { category }), page: String(page + 1) })}>Next →</button></nav></section>
}

function Daily({ ranked, onBoost }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void }) {
  const [category, setCategory] = useState<CreatorCategory | 'all'>('all')
  const daily = useMemo(() => ranked.filter(c => category === 'all' || c.category === category).sort((a,b) => b.dailyContributedCents-a.dailyContributedCents).map((c,i) => ({...c, rank:i+1, totalContributedCents:c.dailyContributedCents})), [ranked, category])
  return <section className="page-shell"><BoardHeader eyebrow="Resets daily at 00:00 UTC" title="Today’s ranking" copy="A live view of the creators receiving the most support today." control={<CategoryFilter value={category} onChange={setCategory} />} /><RankingList creators={daily} onBoost={onBoost} /></section>
}

function Categories({ ranked }: { ranked: readonly RankedCreator[] }) {
  return <section className="page-shell"><BoardHeader eyebrow="Explore CrownRank" title="Creator categories" copy="Find the leading creators in every corner of the creator economy." /><div className="category-grid">{creatorCategories.map(category => { const leaders = ranked.filter(c => c.category === category).slice(0,3); return <Link className="category-card" to={`/rankings?category=${category}`} key={category}><div><span className="category-icon">♛</span><h2>{creatorCategoryLabels[category]}</h2></div><span>{ranked.filter(c => c.category === category).length} creators</span>{leaders.length > 0 ? <ol>{leaders.map((creator,i) => <li key={creator.id}><b>#{i+1}</b><img src={creator.imageUrl} alt="" /><span>{creator.displayName}</span><strong>{formatCurrency(creator.totalContributedCents)}</strong></li>)}</ol> : <p>Be the first in this category.</p>}</Link> })}</div></section>
}

function Profile({ ranked, onBoost }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void }) {
  const { id } = useParams()
  const creator = ranked.find(c => c.id === id)
  if (!creator) return <Navigate to="/rankings" replace />
  return <section className="profile-page"><Link className="back-link" to="/rankings">← Back to ranking</Link><div className="profile-hero"><img src={creator.imageUrl} alt={creator.displayName} /><div className="profile-copy"><span className="category-pill">{creatorCategoryLabels[creator.category]}</span><h1>{creator.firstName} {creator.lastName}</h1><p className="profile-handle">@{creator.username}</p><p>{creator.bio}</p><div className="profile-meta"><span>📍 {creator.location}</span><span>Joined {new Date(creator.joinedAt).toLocaleDateString('en-US',{month:'long',year:'numeric'})}</span></div><div className="social-profile-links">{creator.socialProfiles.map(profile => <a href={profile.url} target="_blank" rel="noreferrer" key={profile.id}>{profile.platform} ↗</a>)}</div></div><aside className="profile-rank"><p>Global rank</p><strong>#{creator.rank}</strong><p>Total score</p><b>{formatCurrency(creator.totalContributedCents)}</b><button className="primary-button full" onClick={() => onBoost(creator)}>Boost this creator</button><small>Anyone can boost. No account needed.</small></aside></div></section>
}

export function App() {
  const ranked = useMemo(() => rankCreators(dummyCreators), [])
  const [entryOpen, setEntryOpen] = useState(false)
  const [boostCreator, setBoostCreator] = useState<Creator>()
  return <BrowserRouter><Layout onEnter={() => setEntryOpen(true)}><Routes><Route path="/" element={<Home ranked={ranked} onBoost={setBoostCreator} onEnter={() => setEntryOpen(true)} />} /><Route path="/rankings" element={<Rankings ranked={ranked} onBoost={setBoostCreator} />} /><Route path="/daily" element={<Daily ranked={ranked} onBoost={setBoostCreator} />} /><Route path="/categories" element={<Categories ranked={ranked} />} /><Route path="/creators/:id" element={<Profile ranked={ranked} onBoost={setBoostCreator} />} /><Route path="*" element={<Navigate to="/" replace />} /></Routes></Layout><EnterRankingDialog isOpen={entryOpen} paymentGateway={appServices.paymentGateway} onClose={() => setEntryOpen(false)} /><BoostDialog creator={boostCreator} gateway={appServices.paymentGateway} onClose={() => setBoostCreator(undefined)} /></BrowserRouter>
}
