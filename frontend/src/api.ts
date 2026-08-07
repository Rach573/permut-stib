export type RegisterPayload = {
  matricule: string
  phoneNumber: string
  password: string
}

export type LoginPayload = {
  identifier: string
  password: string
}

export type Session = { id: string; matricule: string; role: 'Agent' | 'Admin' }
export type DatePeriod = { from: string; to: string }
export type PermutationProposal = { id: string; partnerId: string; offeredPeriod: DatePeriod; status: string }
export type Permutation = {
  id: string; requesterId: string; ownedPeriod: DatePeriod; wantedPeriod: DatePeriod; status: string
  acceptedProposalId?: string; requesterConfirmed: boolean; partnerConfirmed: boolean; proposals: PermutationProposal[]
}
export type SignatureOffer = { id: string; signerId: string; status: string }
export type Signature = {
  id: string; requesterId: string; serviceDate: string; comment?: string; status: string; signerId?: string; offers: SignatureOffer[]
}

async function request<T>(url: string, options: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers ?? {}),
    },
  })

  if (!response.ok) {
    const body = await response.json().catch(() => ({ error: 'Une erreur est survenue.' }))
    throw new Error(body.error ?? 'Une erreur est survenue.')
  }

  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  register: (payload: RegisterPayload) => request('/api/auth/register', { method: 'POST', body: JSON.stringify(payload) }),
  login: (payload: LoginPayload) => request('/api/auth/login', { method: 'POST', body: JSON.stringify(payload) }),
  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),
  me: () => request<Session>('/api/auth/me', { method: 'GET' }),
  myPermutations: () => request<Permutation[]>('/api/permutations/mine', { method: 'GET' }),
  availablePermutations: () => request<Permutation[]>('/api/permutations/available', { method: 'GET' }),
  createPermutation: (ownedPeriod: DatePeriod, wantedPeriod: DatePeriod) => request<Permutation>('/api/permutations', { method: 'POST', body: JSON.stringify({ ownedPeriod, wantedPeriod }) }),
  proposePermutation: (id: string, offeredPeriod: DatePeriod) => request<Permutation>(`/api/permutations/${id}/proposals`, { method: 'POST', body: JSON.stringify(offeredPeriod) }),
  acceptProposal: (id: string, proposalId: string) => request<Permutation>(`/api/permutations/${id}/proposals/${proposalId}/accept`, { method: 'POST' }),
  confirmPermutation: (id: string) => request<Permutation>(`/api/permutations/${id}/confirm`, { method: 'POST' }),
  cancelPermutation: (id: string) => request<void>(`/api/permutations/${id}/cancel`, { method: 'POST' }),
  mySignatures: () => request<Signature[]>('/api/signatures/mine', { method: 'GET' }),
  availableSignatures: () => request<Signature[]>('/api/signatures/available', { method: 'GET' }),
  createSignature: (serviceDate: string, comment: string) => request<Signature>('/api/signatures', { method: 'POST', body: JSON.stringify({ serviceDate, comment: comment || null }) }),
  offerSignature: (id: string) => request<Signature>(`/api/signatures/${id}/offers`, { method: 'POST' }),
  confirmSigner: (id: string, offerId: string) => request<Signature>(`/api/signatures/${id}/offers/${offerId}/confirm`, { method: 'POST' }),
  cancelSignature: (id: string) => request<void>(`/api/signatures/${id}/cancel`, { method: 'POST' }),
}
