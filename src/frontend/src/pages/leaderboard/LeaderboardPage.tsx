import { useEffect, useMemo, useState } from 'react'
import { EnterRankingDialog } from '../../features/creator-entry/ui/EnterRankingDialog'
import { rankCreators } from '../../features/leaderboard/application/rankCreators'
import type { Creator, SocialPlatform } from '../../features/leaderboard/domain/creator'
import { appServices } from '../../shared/config/appServices'
import { formatCurrency } from '../../shared/format/currency'

const socialLabels: Record<SocialPlatform, string> = { instagram: 'IG', tiktok: 'TT', youtube: 'YT', x: 'X', twitch: 'TW', onlyfans: 'OF', website: 'WEB' }

export function LeaderboardPage() {
  const [creators, setCreators] = useState<readonly Creator[]>([])
  const [isEntryOpen, setIsEntryOpen] = useState(false)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => { appServices.leaderboardRepository.getAll().then(setCreators).finally(() => setIsLoading(false)) }, [])
  const rankedCreators = useMemo(() => rankCreators(creators), [creators])

  return (
    <main>
      <nav className="nav" aria-label="Primary navigation"><a className="brand" href="/">CrownRank</a><div className="nav-actions"><a href="#leaderboard">Leaderboard</a><a href="#how-it-works">How it works</a><button className="primary-button compact" type="button" onClick={() => setIsEntryOpen(true)}>Enter the ranking</button></div></nav>

      <section className="hero"><div className="hero-copy"><p className="eyebrow">The fan-powered creator leaderboard</p><h1>Earn attention.<br /><span>Claim the crown.</span></h1><p className="intro">Join with any amount. Rally your audience. Rise through a transparent ranking shaped by real support.</p><div className="hero-actions"><button className="primary-button large" type="button" onClick={() => setIsEntryOpen(true)}>Enter the ranking <span>→</span></button><a href="#leaderboard">Explore leaderboard</a></div><div className="trust-row"><span>No account required</span><span>Choose your amount</span><span>Creator-first platform</span></div></div><div className="crown-card" aria-hidden="true"><span className="mini-label">Current crown</span><span className="crown-glyph">♛</span><strong>{rankedCreators[0]?.displayName ?? 'The top is open'}</strong><small>{rankedCreators[0] ? formatCurrency(rankedCreators[0].totalContributedCents) : 'Be the first to rank'}</small></div></section>

      <section className="leaderboard" id="leaderboard" aria-labelledby="leaderboard-title"><div className="section-heading"><div><p className="eyebrow">Live leaderboard</p><h2 id="leaderboard-title">Creators on the rise</h2></div><span className="live"><i /> Updated live</span></div><div className="table-header" aria-hidden="true"><span>Rank</span><span>Creator</span><span>Supporters</span><span>Score</span><span /></div>{isLoading ? <div className="loading-state">Loading the leaderboard…</div> : <ol className="ranking-list">{rankedCreators.map((creator) => <li className={creator.rank <= 3 ? `top-${creator.rank}` : ''} key={creator.id}><span className="rank">{creator.rank <= 3 ? '♛' : `#${creator.rank}`}<small>#{creator.rank}</small></span><div className="creator-cell"><img src={creator.imageUrl} alt="" /><div><strong>{creator.displayName}</strong><span>@{creator.username}</span><div className="social-list">{creator.socialProfiles.map((profile) => <a key={profile.id} href={profile.url} target="_blank" rel="noreferrer" aria-label={`${creator.displayName} on ${profile.platform}`}>{socialLabels[profile.platform]}</a>)}</div></div></div><span className="supporters">{creator.supporterCount}<small>supporters</small></span><span className="amount">{formatCurrency(creator.totalContributedCents)}<small>total score</small></span><button className="boost-button" type="button" aria-label={`Boost ${creator.displayName}`}>Boost <span>＋</span></button></li>)}</ol>}</section>

      <section className="how-it-works" id="how-it-works"><div><p className="eyebrow">Simple by design</p><h2>Your profile. Your audience. Your momentum.</h2></div><ol><li><span>01</span><strong>Create your entry</strong><p>Add a username, a profile image, and at least one social link.</p></li><li><span>02</span><strong>Choose your amount</strong><p>Start with any contribution that feels right. There is no fixed entry price.</p></li><li><span>03</span><strong>Build your rank</strong><p>Share your page so fans can contribute and help you climb.</p></li></ol></section>
      <footer><a className="brand" href="/">CrownRank</a><p>Where creators rise together.</p><span>© 2026 CrownRank</span></footer>
      <EnterRankingDialog isOpen={isEntryOpen} paymentGateway={appServices.paymentGateway} onClose={() => setIsEntryOpen(false)} />
    </main>
  )
}

