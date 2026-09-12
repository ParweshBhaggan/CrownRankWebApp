import { useCreators } from '../features/leaderboard/application/useCreators'
import { About, Rules, Faq, Terms, Privacy } from '../pages/InformationPages'
import { useCallback, useMemo, useState, type ReactNode } from 'react'
import { BrowserRouter, Link, NavLink, Navigate, Route, Routes, useParams, useSearchParams } from 'react-router-dom'
import { EnterRankingDialog } from '../features/creator-entry/ui/EnterRankingDialog'
import { rankCreators } from '../features/leaderboard/application/rankCreators'
import { creatorCategories, creatorCategoryLabels, type Creator, type CreatorCategory, type RankedCreator } from '../features/leaderboard/domain/creator'
import { RankingList } from '../features/leaderboard/ui/RankingList'
import { BoostDialog } from '../features/payments/ui/BoostDialog'
import { appServices } from '../shared/config/appServices'
import { formatCurrency } from '../shared/format/currency'
import { archivedDateKeys, resolveDailyDate, todayKey } from '../features/leaderboard/application/dailyDates'
import { PaymentCancelled, PaymentComplete } from '../features/payments/ui/PaymentReturnPages'

function Layout({ children, onEnter }: { children: ReactNode; onEnter: () => void }) {
  const [menuOpen, setMenuOpen] = useState(false)
  return <><header className="site-header"><nav className="nav"><Link className="brand" to="/"><span>♛</span>CrownRank</Link><button className="menu-toggle" onClick={() => setMenuOpen(!menuOpen)} aria-label="Toggle navigation" aria-expanded={menuOpen} aria-controls="main-navigation">☰</button><div id="main-navigation" onClick={() => setMenuOpen(false)} className={`nav-actions ${menuOpen ? 'open' : ''}`}><NavLink to="/">Home</NavLink><NavLink to="/rankings">Global rank</NavLink><NavLink to="/daily">Daily rank</NavLink><NavLink to="/categories">Categories</NavLink><NavLink to="/about">About</NavLink><NavLink to="/rules">Rules</NavLink><button className="primary-button compact" onClick={onEnter}>Enter ranking</button></div></nav></header><main>{children}</main><footer><Link className="brand" to="/"><span>♛</span>CrownRank</Link><div className="footer-links"><Link to="/about">About</Link><Link to="/rules">Rules</Link><Link to="/faq">FAQ</Link><Link to="/terms">Terms</Link><Link to="/privacy">Privacy</Link></div><span>© 2026 CrownRank</span></footer></>
}

function CategoryFilter({ value, onChange }: { value: CreatorCategory | 'all'; onChange: (value: CreatorCategory | 'all') => void }) {
  return <label className="filter-select"><span>Category</span><select value={value} onChange={(e) => onChange(e.target.value as CreatorCategory | 'all')}><option value="all">All creators</option>{creatorCategories.map(category => <option value={category} key={category}>{creatorCategoryLabels[category]}</option>)}</select></label>
}

function BoardHeader({ eyebrow, title, copy, control }: { eyebrow: string; title: string; copy: string; control?: ReactNode }) {
  return <div className="board-heading"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p>{copy}</p></div>{control}</div>
}

function Home({ ranked, onBoost, onEnter, revision }: { revision: number; ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void; onEnter: () => void }) {
  const [limit, setLimit] = useState(5)
  return <>
    <section className="hero"><div className="hero-copy"><p className="eyebrow">The fan-powered creator leaderboard</p><h1>Rise through the ranks.<br /><span>Claim the crown.</span></h1><p className="intro">Creators enter with any amount. Communities boost who they believe in. Every score is visible.</p><div className="hero-actions"><button className="primary-button large" onClick={onEnter}>Enter the ranking <span>→</span></button><Link to="/rankings">View global rank</Link></div><div className="trust-row"><span>No account required</span><span>Any starting amount</span><span>Transparent ranking</span></div></div>{ranked[0] && <div className="crown-card"><span className="mini-label">Current world #1</span><img src={ranked[0].imageUrl} alt="" /><strong>{ranked[0].name}</strong><small>@{ranked[0].username}</small><b>{formatCurrency(ranked[0].totalContributed)}</b></div>}</section>
    <section className="board-section"><div className="section-heading"><div><p className="eyebrow">Global ranking</p><h2>Top creators worldwide</h2></div><Link to="/rankings">Full ranking →</Link></div><RankingList creators={ranked.slice(0, limit)} onBoost={onBoost} compact />{limit === 5 ? <button className="secondary-button board-more" onClick={() => setLimit(10)}>See top 10</button> : <Link className="secondary-button board-more" to="/rankings">See full ranking</Link>}</section>
    <section className="daily-preview"><div><p className="eyebrow">Today’s leaders</p><h2>Who is rising today?</h2><p>Daily scores reset at midnight UTC, giving every creator a fresh chance to lead.</p><Link className="text-link" to="/daily">See daily ranking →</Link></div><DailyPreview onBoost={onBoost} revision={revision} /></section>
  </>
}

function DailyPreview({ onBoost, revision }: { onBoost: (c: Creator) => void; revision: number }) {
  const data = useCreators(`/api/rankings/daily/${todayKey()}`, revision)
  if (data.loading) return <p role="status">Loading today’s leaders…</p>
  if (data.error) return <p role="alert">{data.error} <button onClick={data.retry}>Retry</button></p>
  return <RankingList creators={rankCreators(data.creators).slice(0, 3)} onBoost={onBoost} compact />
}

function Rankings({ ranked, onBoost }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void }) {
  const [params, setParams] = useSearchParams()
  const candidateCategory = params.get('category')
  const category = creatorCategories.includes(candidateCategory as CreatorCategory) ? candidateCategory as CreatorCategory : 'all'
  const requestedPage = Number(params.get('page') ?? 1)
  const filtered = category === 'all' ? ranked : rankCreators(ranked.filter(c => c.category === category))
  const pageSize = 25, pages = Math.max(1, Math.ceil(filtered.length / pageSize))
  const page = Number.isSafeInteger(requestedPage) ? Math.min(pages, Math.max(1, requestedPage)) : 1
  const visible = filtered.slice((page - 1) * pageSize, page * pageSize)
  return <section className="page-shell"><BoardHeader eyebrow="All-time leaderboard" title="Global ranking" copy="The complete CrownRank board, ordered by total confirmed contributions." control={<CategoryFilter value={category} onChange={(next) => setParams(next === 'all' ? {} : { category: next })} />} /><RankingList creators={visible} onBoost={onBoost} /><nav className="pagination" aria-label="Ranking pages"><button disabled={page <= 1} onClick={() => setParams({ ...(category !== 'all' && { category }), page: String(page - 1) })}>← Previous</button><span>Page {Math.min(page,pages)} of {pages}</span><button disabled={page >= pages} onClick={() => setParams({ ...(category !== 'all' && { category }), page: String(page + 1) })}>Next →</button></nav></section>
}

function Daily({ onBoost, onEnter, revision }: { revision: number; onBoost: (c: Creator) => void; onEnter: () => void }) {
  const { date } = useParams()
  const selectedDate = resolveDailyDate(date)
  const isToday = selectedDate === todayKey()
  const [category, setCategory] = useState<CreatorCategory | 'all'>('all')
  const { creators, loading, error, retry } = useCreators(`/api/rankings/daily/${selectedDate}`, revision)
  const daily = useMemo(() => rankCreators(creators.filter(c => category === 'all' || c.category === category)), [creators, category])
  return <section className="page-shell"><BoardHeader eyebrow={isToday ? 'Live · resets at 00:00 UTC' : 'Daily archive'} title={isToday ? 'Today’s ranking' : new Date(`${selectedDate}T12:00:00Z`).toLocaleDateString('en-GB', { weekday:'long', day:'numeric', month:'long', year:'numeric' })} copy={isToday ? 'See every creator receiving support today, or revisit the leaders from a previous date.' : 'Contributions confirmed on this date, excluding hidden profiles.'} control={<CategoryFilter value={category} onChange={setCategory} />} /><div className="daily-toolbar"><div className="date-strip" aria-label="Choose ranking date">{archivedDateKeys().slice(0,7).map(key => <Link className={key === selectedDate ? 'selected' : ''} to={key === todayKey() ? '/daily' : `/daily/${key}`} key={key}><b>{key === todayKey() ? 'Today' : new Date(`${key}T12:00:00Z`).toLocaleDateString('en-GB',{weekday:'short'})}</b><span>{new Date(`${key}T12:00:00Z`).toLocaleDateString('en-GB',{day:'numeric',month:'short'})}</span></Link>)}</div>{isToday && <button className="primary-button compact" onClick={onEnter}>Rank up</button>}</div>{loading ? <p className="loading-state">Loading this day…</p> : error ? <p role="alert">{error} <button onClick={retry}>Retry</button></p> : <RankingList creators={daily} onBoost={onBoost} />}<DailyArchive dates={archivedDateKeys().slice(1)} selectedDate={selectedDate} /></section>
}

function DailyArchive({ dates, selectedDate }: { dates: readonly string[]; selectedDate: string }) {
  return <section className="archive-section"><div><p className="eyebrow">Past results</p><h2>Daily archive</h2></div><div className="archive-grid">{dates.map(date => <Link className={date === selectedDate ? 'selected' : ''} to={`/daily/${date}`} key={date}><span>{new Date(`${date}T12:00:00Z`).toLocaleDateString('en-GB',{weekday:'long'})}</span><strong>{new Date(`${date}T12:00:00Z`).toLocaleDateString('en-GB',{day:'numeric',month:'long',year:'numeric'})}</strong><small>Show all →</small></Link>)}</div></section>
}

function Categories({ ranked }: { ranked: readonly RankedCreator[] }) {
  return <section className="page-shell"><BoardHeader eyebrow="Explore CrownRank" title="Creator categories" copy="Find the leading creators in every corner of the creator economy." /><div className="category-grid">{creatorCategories.map(category => { const leaders = ranked.filter(c => c.category === category).slice(0,3); return <Link className="category-card" to={`/rankings?category=${category}`} key={category}><div><span className="category-icon">♛</span><h2>{creatorCategoryLabels[category]}</h2></div><span>{ranked.filter(c => c.category === category).length} creators</span>{leaders.length > 0 ? <ol>{leaders.map((creator,i) => <li key={creator.id}><b>#{i+1}</b><img src={creator.imageUrl} alt="" /><span>{creator.name}</span><strong>{formatCurrency(creator.totalContributed)}</strong></li>)}</ol> : <p>Be the first in this category.</p>}</Link> })}</div></section>
}

function Profile({ ranked, onBoost }: { ranked: readonly RankedCreator[]; onBoost: (c: Creator) => void }) {
  const { id } = useParams()
  const creator = ranked.find(c => c.id === id)
  if (!creator) return <section className="page-shell"><h1>Creator not found</h1><Link to="/rankings">Back to rankings</Link></section>
  return <section className="profile-page"><Link className="back-link" to="/rankings">← Back to ranking</Link><div className="profile-hero"><img src={creator.imageUrl} alt={creator.name} /><div className="profile-copy"><span className="category-pill">{creatorCategoryLabels[creator.category]}</span><h1>{creator.name}</h1><p className="profile-handle">@{creator.username}</p><div className="profile-meta"><span>Joined {new Date(creator.joinedAt).toLocaleDateString('en-US',{month:'long',year:'numeric'})}</span></div><div className="social-profile-links">{creator.socialProfiles.map(profile => <a href={profile.url} target="_blank" rel="noreferrer" key={profile.id}>{profile.platform} ↗</a>)}</div></div><aside className="profile-rank"><p>Global rank</p><strong>#{creator.rank}</strong><p>Total score</p><b>{formatCurrency(creator.totalContributed)}</b><button className="primary-button full" onClick={() => onBoost(creator)}>Boost this creator</button><small>Anyone can boost. No account needed.</small></aside></div></section>
}

export function App() {
  const [revision, setRevision] = useState(0)
  const { creators, loading: isLoading, error: loadError, retry } = useCreators('/api/creators', revision)
  const ranked = useMemo(() => rankCreators(creators), [creators])
  const confirmed = useCallback(() => setRevision(value => value + 1), [])
  const [entryOpen, setEntryOpen] = useState(false)
  const [entryVersion, setEntryVersion] = useState(0)
  const [boostCreator, setBoostCreator] = useState<Creator>()
  const boardState = <section className="page-shell api-state"><p className="eyebrow">CrownRank API</p><h1>{loadError ? 'Leaderboard unavailable' : 'Loading the ranking…'}</h1><p>{loadError ? 'Please try again in a moment.' : 'Fetching the latest confirmed scores.'}</p>{loadError && <button onClick={retry}>Retry</button>}</section>
  const ready = !isLoading && !loadError
  return <BrowserRouter><Layout onEnter={() => setEntryOpen(true)}><Routes><Route path="/" element={ready ? <Home revision={revision} ranked={ranked} onBoost={setBoostCreator} onEnter={() => setEntryOpen(true)} /> : boardState} /><Route path="/rankings" element={ready ? <Rankings ranked={ranked} onBoost={setBoostCreator} /> : boardState} /><Route path="/daily" element={<Daily revision={revision} onBoost={setBoostCreator} onEnter={() => setEntryOpen(true)} />} /><Route path="/daily/:date" element={<Daily revision={revision} onBoost={setBoostCreator} onEnter={() => setEntryOpen(true)} />} /><Route path="/categories" element={ready ? <Categories ranked={ranked} /> : boardState} /><Route path="/creators/:id" element={ready ? <Profile ranked={ranked} onBoost={setBoostCreator} /> : boardState} /><Route path="/payment/complete" element={<PaymentComplete onConfirmed={confirmed} />} /><Route path="/payment/cancel" element={<PaymentCancelled />} /><Route path="/about" element={<About />} /><Route path="/rules" element={<Rules />} /><Route path="/faq" element={<Faq />} /><Route path="/terms" element={<Terms />} /><Route path="/privacy" element={<Privacy />} /><Route path="*" element={<Navigate to="/" replace />} /></Routes></Layout><EnterRankingDialog key={entryVersion} isOpen={entryOpen} onConfirmed={confirmed} onClose={(completed) => { setEntryOpen(false); if (completed) setEntryVersion(value => value + 1) }} />{boostCreator && <BoostDialog onConfirmed={confirmed} creator={boostCreator} gateway={appServices.paymentGateway} onClose={() => setBoostCreator(undefined)} />}</BrowserRouter>
}
