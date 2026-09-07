const previewCreators = [
  { rank: 1, name: 'Your name here', amount: '$100+' },
  { rank: 2, name: 'Claim the crown', amount: '$100+' },
  { rank: 3, name: 'Rise with your fans', amount: '$100+' },
]

export function LeaderboardPage() {
  return (
    <main>
      <nav className="nav" aria-label="Primary navigation">
        <a className="brand" href="/">CrownRank</a>
        <button className="rank-button" type="button">Rank up</button>
      </nav>

      <section className="hero">
        <p className="eyebrow">The fan-powered creator leaderboard</p>
        <h1>Your audience.<br /><span>Your crown.</span></h1>
        <p className="intro">Join the ranking with one social link. No account required. Fans can boost creators they love.</p>
        <button className="rank-button large" type="button">Enter the ranking — $100</button>
      </section>

      <section className="leaderboard" aria-labelledby="leaderboard-title">
        <div className="section-heading">
          <div>
            <p className="eyebrow">Live leaderboard</p>
            <h2 id="leaderboard-title">The crown is waiting</h2>
          </div>
          <span className="live"><i /> Live</span>
        </div>
        <ol>
          {previewCreators.map((creator) => (
            <li key={creator.rank}>
              <span className="rank">#{creator.rank}</span>
              <span className="avatar" aria-hidden="true">♛</span>
              <strong>{creator.name}</strong>
              <span className="amount">{creator.amount}</span>
            </li>
          ))}
        </ol>
      </section>
    </main>
  )
}

