import type { Creator, CreatorCategory, SocialPlatform } from '../domain/creator'

const people = [
  ['Luna','Vale','lunaafterdark','adult-entertainment'],['Maya','Chen','mayamoves','fitness-wellness'],['Nova','Rose','novarose','streamer'],['Aria','Atlas','ariaatlas','travel'],['Jules','Rivera','julescreates','art-design'],
  ['Kai','Morgan','kaiplays','gaming'],['Sofia','Reyes','sofiastyle','beauty-fashion'],['Theo','Brooks','theotech','technology'],['Amara','Stone','amarabeats','music'],['Leo','Hart','leohartlive','influencer'],
  ['Nina','Park','ninaknows','education'],['Owen','Reed','owenlaughs','comedy'],['Zara','Cole','zaracooks','food'],['Milo','James','milotalks','podcasting'],['Ivy','Banks','ivybusiness','business'],
] as const
const images = ['photo-1534528741775-53994a69daeb','photo-1529139574466-a303027c1d8b','photo-1531123897727-8f129e1688ce','photo-1524504388940-b1c1722653e1','photo-1500648767791-00dcc994a43e','photo-1507003211169-0a1dd7228f2d','photo-1494790108377-be9c29b29330','photo-1506794778202-cad84cf45f1d','photo-1531746020798-e6953c6e8e04','photo-1519345182560-3f2917c472ef']
const platformByCategory: Partial<Record<CreatorCategory, SocialPlatform>> = { streamer:'twitch', gaming:'youtube', 'adult-entertainment':'onlyfans', technology:'x', music:'tiktok' }

export const dummyCreators: readonly Creator[] = Array.from({ length: 50 }, (_, index) => {
  const seed = people[index % people.length]
  const cycle = Math.floor(index / people.length)
  const category = seed[3] as CreatorCategory
  const username = `${seed[2]}${cycle || ''}`
  const platform = platformByCategory[category] ?? 'instagram'
  return {
    id: `creator-${index + 1}`, firstName: seed[0], lastName: seed[1], username,
    displayName: `${seed[0]} ${seed[1]}`, category,
    bio: `Independent ${category.replaceAll('-', ' & ')} creator sharing original work, behind-the-scenes moments, and new releases with a growing community.`,
    location: ['Los Angeles, US','London, UK','Toronto, CA','Amsterdam, NL','Sydney, AU'][index % 5],
    imageUrl: `https://images.unsplash.com/${images[index % images.length]}?auto=format&fit=crop&w=480&q=82`,
    socialProfiles: [
      { id: `${username}-${platform}`, platform, url: platform === 'website' ? `https://example.com/${username}` : `https://${platform === 'x' ? 'x.com' : `${platform}.com`}/${platform === 'tiktok' ? '@' : ''}${username}` },
      ...(index % 3 === 0 ? [{ id: `${username}-website`, platform: 'website' as const, url: `https://example.com/${username}` }] : []),
    ],
    totalContributedCents: Math.max(9500, 425000 - index * 7400 - cycle * 350),
    dailyContributedCents: Math.max(0, 86400 - ((index * 13700) % 90000)),
    joinedAt: new Date(Date.UTC(2026, 7, 1 + (index % 31))).toISOString(),
  }
})
