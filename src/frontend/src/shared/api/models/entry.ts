export interface CreateEntryRequest {
  readonly name: string
  readonly username: string
  readonly imgUrl: string
  readonly score: number
  readonly categories: readonly { readonly name: string; readonly description?: string }[]
  readonly socialMediaPlatforms: readonly { readonly platformName: string; readonly url: string }[]
}

export interface ApiEntry extends CreateEntryRequest {
  readonly id: string
  readonly createdDate: string
  readonly updatedDate?: string
}

export interface ApiDailyEntry {
  readonly entry: ApiEntry
  readonly dailyScore: number
  readonly scoreReachedDate: string
}


export interface PaidEntryRequest extends CreateEntryRequest {
  readonly acceptedAgreements: boolean
}
