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
export type SignatureOffer = { id: string; signerId: string; availabilityId?: string; status: string }
export type Signature = {
  id: string; requesterId: string; serviceDate: string; comment?: string; status: string; signerId?: string; offers: SignatureOffer[]
}
export type SignatureAvailability = {
  id: string; agentId: string; serviceDate: string; comment?: string; isActive: boolean; createdAt: string
}
export type AgentNotification = { id: string; type: string; message: string; entityType: string; entityId: string; isRead: boolean; createdAt: string }
export type AdminSummary = { pendingAgents: number; activeAgents: number; suspendedAgents: number; openPermutations: number; confirmedPermutations: number; openSignatures: number; confirmedSignatures: number; auditEvents: number }
export type AdminAgent = { id: string; matricule: string; phoneNumber: string; status: 'Pending' | 'Active' | 'Suspended' | 'Rejected'; role: 'Agent' | 'Admin'; createdAt: string }
export type AdminPermutation = { id: string; requesterMatricule: string; ownedFrom: string; ownedTo: string; wantedFrom: string; wantedTo: string; status: string; proposalCount: number; createdAt: string }
export type AdminSignature = { id: string; requesterMatricule: string; serviceDate: string; comment?: string; status: string; signerMatricule?: string; offerCount: number; createdAt: string }
export type HelpStatistics = { agentId: string; matricule: string; signaturesReceived: number; signaturesGiven: number; signatureOffers: number; helpRatio?: number }
export type AdminAuditEntry = { id: number; entityType: string; entityId: string; action: string; actorMatricule?: string; subjectMatricule?: string; beforeJson?: string; afterJson?: string; reason?: string; createdAt: string }

async function request<T>(url: string, options: RequestInit): Promise<T> {
  const response = await fetch(url, {
    ...options,
    credentials: 'include',
    headers: {
      'Content-Type': 'application/json',
      'X-Permut-STIB': 'app',
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
  mySignatureAvailabilities: () => request<SignatureAvailability[]>('/api/signatures/availabilities/mine', { method: 'GET' }),
  createSignatureAvailability: (serviceDate: string, comment: string) => request<SignatureAvailability>('/api/signatures/availabilities', { method: 'POST', body: JSON.stringify({ serviceDate, comment: comment || null }) }),
  cancelSignatureAvailability: (id: string) => request<void>(`/api/signatures/availabilities/${id}/cancel`, { method: 'POST' }),
  notifications: (unreadOnly = false) => request<AgentNotification[]>(`/api/notifications?unreadOnly=${unreadOnly}`, { method: 'GET' }),
  markNotificationRead: (id: string) => request<void>(`/api/notifications/${id}/read`, { method: 'POST' }),
  markAllNotificationsRead: () => request<void>('/api/notifications/read-all', { method: 'POST' }),
  adminSummary: () => request<AdminSummary>('/api/admin/summary', { method: 'GET' }),
  adminAgents: () => request<AdminAgent[]>('/api/admin/agents', { method: 'GET' }),
  setAgentStatus: (id: string, status: AdminAgent['status'], reason: string) => request<void>(`/api/admin/agents/${id}/status`, { method: 'POST', body: JSON.stringify({ status, reason }) }),
  adminPermutations: () => request<AdminPermutation[]>('/api/admin/permutations', { method: 'GET' }),
  adminSignatures: () => request<AdminSignature[]>('/api/admin/signatures', { method: 'GET' }),
  helpStatistics: () => request<HelpStatistics[]>('/api/admin/help-statistics', { method: 'GET' }),
  adminAudit: () => request<AdminAuditEntry[]>('/api/admin/audit', { method: 'GET' }),
}
