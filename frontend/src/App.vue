<script setup lang="ts">
import { nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { api, type AdminAgent, type AdminAuditEntry, type AdminPermutation, type AdminSignature, type AdminSummary, type AgentNotification, type HelpStatistics, type Permutation, type Session, type Signature, type SignatureAvailability } from './api'
import AppIcon from './components/AppIcon.vue'

type View = 'login' | 'register' | 'pending' | 'home' | 'notifications' | 'permutation-menu' | 'signature-menu' | 'new-permutation' | 'permutations' | 'new-signature' | 'signatures' | 'available-signatures' | 'signature-availabilities'
const view = ref<View>('login')
const busy = ref(false)
const busyLabel = ref('Traitement en cours…')
const error = ref('')
const session = ref<Session | null>(null)
const identifier = ref(''), loginPassword = ref('')
const matricule = ref(''), phoneNumber = ref(''), password = ref(''), passwordConfirmation = ref('')
const ownedFrom = ref(''), ownedTo = ref(''), wantedFrom = ref(''), wantedTo = ref('')
const signatureDate = ref(''), signatureComment = ref('')
const availabilityDate = ref(''), availabilityComment = ref('')
const myPermutations = ref<Permutation[]>([]), availablePermutations = ref<Permutation[]>([])
const mySignatures = ref<Signature[]>([]), availableSignatures = ref<Signature[]>([])
const mySignatureAvailabilities = ref<SignatureAvailability[]>([])
const notifications = ref<AgentNotification[]>([])
const notificationsExpanded = ref(true)
const focusedEntityId = ref<string | null>(null)
const adminSummary = ref<AdminSummary | null>(null)
const adminAgents = ref<AdminAgent[]>([])
const adminPermutations = ref<AdminPermutation[]>([])
const adminSignatures = ref<AdminSignature[]>([])
const helpStatistics = ref<HelpStatistics[]>([])
const adminAudit = ref<AdminAuditEntry[]>([])
const adminTab = ref<'home' | 'agents' | 'exchanges' | 'audit'>('home')
const agentFilter = ref('')
const confirmation = ref<{ title: string; message: string; confirmLabel: string; busyLabel: string; action: () => Promise<void> } | null>(null)
const adminDecisionAgent = ref<AdminAgent | null>(null)
const adminDecisionStatus = ref<AdminAgent['status']>('Pending')
const adminDecisionReason = ref('')
const query = new URLSearchParams(location.search)
const nativeShell = query.get('native') === '1'
const agentPreview = import.meta.env.DEV && query.get('preview') === 'agent'
const splashVisible = ref(!nativeShell)
let restoringHistory = false
let splashFallbackTimer: number | undefined

const frenchStatus: Record<string, string> = {
  Open: 'Ouverte', ProposalReceived: 'Réponse reçue', Accepted: 'Accord accepté', Confirmed: 'Confirmation en cours',
  Locked: 'Confirmée', Cancelled: 'Annulée', Rejected: 'Refusée', Pending: 'En attente', Active: 'Actif',
  Suspended: 'Suspendu', Selected: 'Choisi', Withdrawn: 'Retirée',
}

const frenchAction: Record<string, string> = {
  Created: 'Création', ProposalCreated: 'Proposition envoyée', ProposalAccepted: 'Proposition acceptée',
  Confirmed: 'Confirmation', Locked: 'Confirmation définitive', Cancelled: 'Annulation', SignerOffered: 'Signataire proposé',
  AvailabilityCreated: 'Disponibilité ajoutée', StatusChanged: 'Statut modifié', DemoCreated: 'Donnée de démonstration',
  Activated: 'Compte activé', Suspended: 'Compte suspendu', Rejected: 'Compte refusé',
}

function statusLabel(status: string) { return frenchStatus[status] ?? status.replace(/([a-z])([A-Z])/g, '$1 $2') }
function statusClass(status: string) { return `status-${status.toLowerCase()}` }
function actionLabel(action: string) { return frenchAction[action] ?? action.replace(/([a-z])([A-Z])/g, '$1 $2') }
function entityLabel(entity: string) { return entity === 'Permutation' ? 'Permutation' : entity === 'Signature' ? 'Signature' : entity === 'SignatureAvailability' ? 'Disponibilité de signature' : entity === 'Agent' ? 'Agent' : entity }
function formatDate(value: string) {
  const date = new Date(`${value.slice(0, 10)}T00:00:00`)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('fr-BE', { day: 'numeric', month: 'short', year: 'numeric' }).format(date)
}
function periodLabel(kind: 'owned' | 'wanted') { return kind === 'owned' ? 'Période proposée' : 'Période souhaitée' }

function finishSplash() {
  splashVisible.value = false
  if (splashFallbackTimer !== undefined) window.clearTimeout(splashFallbackTimer)
}

async function execute(action: () => Promise<void>, label = 'Traitement en cours…') {
  error.value = ''; busy.value = true; busyLabel.value = label
  try { await action() } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Action impossible.' }
  finally { busy.value = false }
}

function requestConfirmation(title: string, message: string, confirmLabel: string, busyText: string, action: () => Promise<void>) {
  error.value = ''
  confirmation.value = { title, message, confirmLabel, busyLabel: busyText, action }
}

async function runConfirmedAction() {
  const pending = confirmation.value
  if (!pending) return
  confirmation.value = null
  await execute(pending.action, pending.busyLabel)
}

async function login() { await execute(async () => { session.value = await api.login({ identifier: identifier.value, password: loginPassword.value }) as Session; view.value = 'home'; if (session.value.role === 'Admin') await loadAdminData(); else notifications.value = await api.notifications() }, 'Connexion en cours…') }
async function register() {
  if (password.value !== passwordConfirmation.value) { error.value = 'Les mots de passe ne correspondent pas.'; return }
  await execute(async () => { await api.register({ matricule: matricule.value, phoneNumber: phoneNumber.value, password: password.value }); view.value = 'pending' }, 'Création du compte…')
}
async function logout() { await execute(async () => { await api.logout(); session.value = null; view.value = 'login' }, 'Déconnexion…') }
async function loadPermutations() {
  if (agentPreview) { view.value = 'permutations'; return }
  await execute(async () => { [myPermutations.value, availablePermutations.value] = await Promise.all([api.myPermutations(), api.availablePermutations()]); view.value = 'permutations' }, 'Chargement des permutations…')
}
async function createPermutation() {
  if (agentPreview) {
    myPermutations.value = [{
      id: `preview-permutation-${Date.now()}`, requesterId: session.value!.id,
      ownedPeriod: { from: ownedFrom.value, to: ownedTo.value }, wantedPeriod: { from: wantedFrom.value, to: wantedTo.value },
      status: 'Open', requesterConfirmed: false, partnerConfirmed: false, proposals: [],
    }, ...myPermutations.value]
    ownedFrom.value = ''; ownedTo.value = ''; wantedFrom.value = ''; wantedTo.value = ''; view.value = 'permutations'
    return
  }
  await execute(async () => { await api.createPermutation({ from: ownedFrom.value, to: ownedTo.value }, { from: wantedFrom.value, to: wantedTo.value }); await loadPermutations() }, 'Publication de la demande…')
}
async function proposePermutation(item: Permutation) {
  if (agentPreview) {
    myPermutations.value = [{ ...item, status: 'ProposalReceived', proposals: [{ id: 'preview-new-proposal', partnerId: 'preview-agent', offeredPeriod: item.wantedPeriod, status: 'Pending' }] }, ...myPermutations.value]
    availablePermutations.value = availablePermutations.value.filter(candidate => candidate.id !== item.id)
    return
  }
  await execute(async () => { await api.proposePermutation(item.id, item.wantedPeriod); await loadPermutations() }, 'Envoi de la proposition…')
}
async function acceptProposal(item: Permutation, proposalId: string) { await execute(async () => { await api.acceptProposal(item.id, proposalId); await loadPermutations() }, 'Acceptation de la proposition…') }
async function confirmPermutation(item: Permutation) { await execute(async () => { await api.confirmPermutation(item.id); await loadPermutations() }, 'Confirmation de l’échange…') }
function cancelPermutation(item: Permutation) {
  requestConfirmation('Annuler cette permutation ?', 'La demande ne sera plus visible par les autres agents.', 'Annuler la permutation', 'Annulation de la permutation…', async () => {
    if (agentPreview) { myPermutations.value = myPermutations.value.filter(candidate => candidate.id !== item.id); return }
    await api.cancelPermutation(item.id); await loadPermutations()
  })
}
async function loadMySignatures() {
  if (agentPreview) { view.value = 'signatures'; return }
  await execute(async () => { mySignatures.value = await api.mySignatures(); view.value = 'signatures' }, 'Chargement de mes signatures…')
}
async function loadAvailableSignatures() {
  if (agentPreview) { view.value = 'available-signatures'; return }
  await execute(async () => { availableSignatures.value = await api.availableSignatures(); view.value = 'available-signatures' }, 'Chargement des demandes…')
}
async function createSignature() {
  if (agentPreview) {
    mySignatures.value = [{ id: `preview-signature-${Date.now()}`, requesterId: session.value!.id, serviceDate: signatureDate.value, comment: signatureComment.value, status: 'Open', offers: [] }, ...mySignatures.value]
    signatureDate.value = ''; signatureComment.value = ''; view.value = 'signatures'
    return
  }
  await execute(async () => { await api.createSignature(signatureDate.value, signatureComment.value); signatureDate.value = ''; signatureComment.value = ''; mySignatures.value = await api.mySignatures(); view.value = 'signatures' }, 'Publication de la demande…')
}
async function offerSignature(item: Signature) {
  if (agentPreview) {
    mySignatures.value = [{ ...item, status: 'ProposalReceived', offers: [{ id: 'preview-offer-sent', signerId: 'preview-agent', status: 'Pending' }] }, ...mySignatures.value]
    availableSignatures.value = availableSignatures.value.filter(candidate => candidate.id !== item.id)
    view.value = 'signatures'
    return
  }
  await execute(async () => { await api.offerSignature(item.id); mySignatures.value = await api.mySignatures(); availableSignatures.value = await api.availableSignatures(); view.value = 'signatures' }, 'Envoi de la proposition…')
}
async function confirmSigner(item: Signature, offerId: string) { await execute(async () => { await api.confirmSigner(item.id, offerId); mySignatures.value = await api.mySignatures() }, 'Confirmation du signataire…') }
function cancelSignature(item: Signature) {
  requestConfirmation('Annuler cette demande ?', 'Les propositions de signature associées seront également annulées.', 'Annuler la demande', 'Annulation de la signature…', async () => {
    if (agentPreview) { mySignatures.value = mySignatures.value.filter(candidate => candidate.id !== item.id); return }
    await api.cancelSignature(item.id); mySignatures.value = await api.mySignatures()
  })
}
async function loadSignatureAvailabilities() {
  if (agentPreview) { view.value = 'signature-availabilities'; return }
  await execute(async () => { mySignatureAvailabilities.value = await api.mySignatureAvailabilities(); view.value = 'signature-availabilities' }, 'Chargement des disponibilités…')
}
async function createSignatureAvailability() {
  if (agentPreview) {
    mySignatureAvailabilities.value = [{ id: `preview-availability-${Date.now()}`, agentId: session.value!.id, serviceDate: availabilityDate.value, comment: availabilityComment.value, isActive: true, createdAt: new Date().toISOString() }, ...mySignatureAvailabilities.value]
    availabilityDate.value = ''; availabilityComment.value = ''; view.value = 'signature-availabilities'
    return
  }
  await execute(async () => { await api.createSignatureAvailability(availabilityDate.value, availabilityComment.value); availabilityDate.value = ''; availabilityComment.value = ''; await loadSignatureAvailabilities() }, 'Ajout de la disponibilité…')
}
function cancelSignatureAvailability(item: SignatureAvailability) {
  requestConfirmation('Retirer cette disponibilité ?', 'Elle ne sera plus proposée aux collègues.', 'Retirer la disponibilité', 'Retrait de la disponibilité…', async () => {
    if (agentPreview) { mySignatureAvailabilities.value = mySignatureAvailabilities.value.map(candidate => candidate.id === item.id ? { ...candidate, isActive: false } : candidate); return }
    await api.cancelSignatureAvailability(item.id); await loadSignatureAvailabilities()
  })
}
async function loadNotifications() {
  if (agentPreview) return
  await execute(async () => { notifications.value = await api.notifications() }, 'Chargement des notifications…')
}
async function toggleNotifications() {
  notificationsExpanded.value = !notificationsExpanded.value
  if (notificationsExpanded.value) await loadNotifications()
}
async function showAllNotifications() {
  if (!agentPreview) await loadNotifications()
  view.value = 'notifications'
}
async function markAllRead() {
  if (agentPreview) { notifications.value = notifications.value.map(item => ({ ...item, isRead: true })); return }
  await execute(async () => { await api.markAllNotificationsRead(); notifications.value = await api.notifications() }, 'Mise à jour des notifications…')
}

function currentAgentProposal(item: Permutation) {
  return item.proposals.find(proposal => proposal.partnerId === session.value?.id)
}

function currentAgentSignatureOffer(item: Signature) {
  return item.offers.find(offer => offer.signerId === session.value?.id)
}

async function openNotification(notification: AgentNotification) {
  await execute(async () => {
    if (!agentPreview && !notification.isRead) await api.markNotificationRead(notification.id)
    notifications.value = notifications.value.map(item => item.id === notification.id ? { ...item, isRead: true } : item)

    if (notification.entityType === 'Permutation') {
      if (!agentPreview) [myPermutations.value, availablePermutations.value] = await Promise.all([api.myPermutations(), api.availablePermutations()])
      view.value = 'permutations'
    } else if (notification.entityType === 'Signature') {
      if (!agentPreview) mySignatures.value = await api.mySignatures()
      view.value = 'signatures'
    } else {
      return
    }

    focusedEntityId.value = notification.entityId
    await nextTick()
    document.getElementById(`entity-${notification.entityId}`)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
    window.setTimeout(() => {
      if (focusedEntityId.value === notification.entityId) focusedEntityId.value = null
    }, 2500)
  }, 'Ouverture de la notification…')
}

function initializeAgentPreview() {
  const permutationId = 'preview-permutation-waiting'
  const signatureId = 'preview-signature-match'
  myPermutations.value = [{
    id: permutationId, requesterId: 'preview-colleague', ownedPeriod: { from: '2026-09-07', to: '2026-09-13' },
    wantedPeriod: { from: '2026-10-05', to: '2026-10-11' }, status: 'ProposalReceived', requesterConfirmed: false,
    partnerConfirmed: false, proposals: [{ id: 'preview-proposal', partnerId: 'preview-agent', offeredPeriod: { from: '2026-10-05', to: '2026-10-11' }, status: 'Pending' }],
  }]
  availablePermutations.value = [{
    id: 'preview-permutation-available', requesterId: 'preview-colleague-2', ownedPeriod: { from: '2026-11-02', to: '2026-11-08' },
    wantedPeriod: { from: '2026-12-07', to: '2026-12-13' }, status: 'Open', requesterConfirmed: false, partnerConfirmed: false, proposals: [],
  }]
  mySignatures.value = [{
    id: signatureId, requesterId: 'preview-agent', serviceDate: '2026-09-18', comment: 'Service du matin', status: 'ProposalReceived',
    offers: [{ id: 'preview-signature-offer', signerId: 'preview-colleague-3', availabilityId: 'preview-availability', status: 'Pending' }],
  }]
  availableSignatures.value = [{ id: 'preview-signature-available', requesterId: 'preview-colleague-4', serviceDate: '2026-09-22', comment: 'Service complet', status: 'Open', offers: [] }]
  notifications.value = [
    { id: 'preview-notification-permutation', type: 'PermutationProposalReceived', message: 'Permutation : confirmation en attente.', entityType: 'Permutation', entityId: permutationId, isRead: false, createdAt: new Date().toISOString() },
    { id: 'preview-notification-signature', type: 'SignatureAvailabilityMatched', message: 'Collègue disponible à cette date.', entityType: 'Signature', entityId: signatureId, isRead: false, createdAt: new Date(Date.now() - 60000).toISOString() },
  ]
}

async function loadAdminData() {
  [adminSummary.value, adminAgents.value, adminPermutations.value, adminSignatures.value, helpStatistics.value, adminAudit.value] = await Promise.all([
    api.adminSummary(), api.adminAgents(), api.adminPermutations(), api.adminSignatures(), api.helpStatistics(), api.adminAudit(),
  ])
}

async function changeAgentStatus(agent: AdminAgent, status: AdminAgent['status']) {
  error.value = ''
  adminDecisionAgent.value = agent
  adminDecisionStatus.value = status
  adminDecisionReason.value = ''
}

function closeAdminDecision() {
  adminDecisionAgent.value = null
  adminDecisionReason.value = ''
  error.value = ''
}

async function submitAgentDecision() {
  const agent = adminDecisionAgent.value
  const reason = adminDecisionReason.value.trim()
  if (!agent || !reason) { error.value = 'Le motif est obligatoire.'; return }
  await execute(async () => {
    await api.setAgentStatus(agent.id, adminDecisionStatus.value, reason)
    adminDecisionAgent.value = null
    await loadAdminData()
  }, 'Enregistrement de la décision…')
}

function filteredAgents() {
  const value = agentFilter.value.trim().toLowerCase()
  return value ? adminAgents.value.filter(x => x.matricule.toLowerCase().includes(value) || x.status.toLowerCase().includes(value)) : adminAgents.value
}

function restoreFromHistory(event: PopStateEvent) {
  if (!event.state?.view) return
  restoringHistory = true
  view.value = event.state.view
  adminTab.value = event.state.adminTab ?? 'home'
  queueMicrotask(() => { restoringHistory = false })
}

watch([view, adminTab], ([nextView, nextAdminTab]) => {
  error.value = ''
  if (!restoringHistory) history.pushState({ view: nextView, adminTab: nextAdminTab }, '')
})

onMounted(async () => {
  history.replaceState({ view: view.value, adminTab: adminTab.value }, '')
  window.addEventListener('popstate', restoreFromHistory)
  if (splashVisible.value) splashFallbackTimer = window.setTimeout(finishSplash, 7000)
  if (import.meta.env.DEV && query.get('preview') === 'admin') {
    session.value = { id: 'preview', matricule: 'DÉLÉGUÉ', role: 'Admin' }
    view.value = 'home'
    return
  }
  if (agentPreview) {
    session.value = { id: 'preview-agent', matricule: '70-001', role: 'Agent' }
    initializeAgentPreview()
    view.value = 'home'
    return
  }
  try { session.value = await api.me(); view.value = 'home'; if (session.value.role === 'Admin') await loadAdminData(); else notifications.value = await api.notifications() } catch { /* aucune session */ }
})

onBeforeUnmount(() => {
  window.removeEventListener('popstate', restoreFromHistory)
  if (splashFallbackTimer !== undefined) window.clearTimeout(splashFallbackTimer)
})
</script>

<template>
  <main class="shell">
    <Transition name="splash-fade"><div v-if="splashVisible" class="splash-screen"><video src="/csc-stib-ouverture.mp4" autoplay muted playsinline preload="auto" aria-label="Animation d'ouverture CSC STIB" @ended="finishSplash" @error="finishSplash" /><button class="skip-splash" type="button" @click="finishSplash">Passer</button></div></Transition>
    <header><span class="mark"><AppIcon name="permutation" /></span><div><strong>Permut' STIB</strong><small>Entraide entre agents</small></div></header>
    <div v-if="busy" class="loading-notice" role="status"><span class="loading-spinner" aria-hidden="true"></span><span>{{ busyLabel }}</span></div>
    <p v-if="error && !['login', 'register', 'new-permutation', 'new-signature', 'signature-availabilities'].includes(view)" class="error notice" role="alert">{{ error }}</p>

    <section v-if="view === 'login'" class="panel">
      <h1>Se connecter</h1><p>Utilise ton matricule STIB ou ton GSM.</p>
      <form @submit.prevent="login"><label>Matricule ou GSM<input v-model="identifier" placeholder="Ex. 70-001 ou 0470 00 00 01" required /></label><label>Mot de passe<input v-model="loginPassword" type="password" placeholder="Votre mot de passe" required /></label><p v-if="error" class="form-error" role="alert">{{ error }}</p><button :disabled="busy">Se connecter</button></form>
      <button class="secondary" @click="view = 'register'">Créer mon compte</button>
    </section>
    <section v-else-if="view === 'register'" class="panel">
      <h1>Créer mon compte</h1><p>Après l’inscription, le délégué doit activer le compte avant la première connexion.</p>
      <form @submit.prevent="register"><label>Matricule<input v-model="matricule" placeholder="Ex. 70-123" minlength="3" required /></label><label>GSM belge<input v-model="phoneNumber" type="tel" placeholder="Ex. 0470 00 00 01" required /></label><label>Mot de passe<input v-model="password" type="password" minlength="8" placeholder="8 caractères minimum" required /></label><small class="form-help">Pour la démonstration, vous pouvez utiliser <b>test1234</b>.</small><label>Confirmer<input v-model="passwordConfirmation" type="password" minlength="8" required /></label><p v-if="error" class="form-error" role="alert">{{ error }}</p><button :disabled="busy">Envoyer l’inscription</button></form>
      <button class="secondary" @click="view = 'login'">Retour</button>
    </section>
    <section v-else-if="view === 'pending'" class="panel"><span class="pending-icon">✓</span><h1>Inscription enregistrée</h1><p>Le compte est maintenant <b>en attente</b>. Connectez-vous avec <b>DELEGUE</b>, ouvrez « Agents », puis appuyez sur « Activer ». L’agent pourra ensuite se connecter.</p><button @click="view = 'login'">Retour à la connexion</button></section>

    <template v-else>
      <section v-if="view === 'home' && session?.role === 'Admin'" class="admin-dashboard">
        <div class="admin-heading"><div><span class="eyebrow">ESPACE DÉLÉGUÉ</span><h1>{{ adminTab === 'home' ? 'Vue d’ensemble' : adminTab === 'agents' ? 'Agents' : adminTab === 'exchanges' ? 'Échanges' : 'Journal d’audit' }}</h1><p>Données réelles de la base de démonstration.</p></div><button class="icon-button" title="Se déconnecter" aria-label="Se déconnecter" @click="logout"><AppIcon name="logout" /></button></div>

        <template v-if="adminTab === 'home' && adminSummary">
          <div class="metric-grid"><article class="metric urgent"><strong>{{ adminSummary.pendingAgents }}</strong><span>Inscriptions<br>en attente</span></article><article class="metric"><strong>{{ adminSummary.openPermutations }}</strong><span>Permutations<br>ouvertes</span></article><article class="metric"><strong>{{ adminSummary.openSignatures }}</strong><span>Signatures<br>recherchées</span></article><article class="metric success"><strong>{{ adminSummary.confirmedPermutations + adminSummary.confirmedSignatures }}</strong><span>Opérations<br>confirmées</span></article></div>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon"><AppIcon name="users" /></span><strong>Inscriptions à valider</strong></div><button class="text-button" @click="adminTab = 'agents'">Tous les agents</button></div><p v-if="!adminAgents.some(x => x.status === 'Pending')" class="hint">Aucune inscription en attente.</p><div class="approval" v-for="agent in adminAgents.filter(x => x.status === 'Pending').slice(0, 5)" :key="agent.id"><div><b>{{ agent.matricule }}</b><small>{{ agent.phoneNumber }} · {{ new Date(agent.createdAt).toLocaleDateString('fr-BE') }}</small></div><div class="inline-actions"><button class="approve" title="Valider" @click="changeAgentStatus(agent, 'Active')">✓</button><button class="reject" title="Refuser" @click="changeAgentStatus(agent, 'Rejected')">×</button></div></div></article>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon"><AppIcon name="help" /></span><strong>Entraide</strong></div><button class="text-button" @click="adminTab = 'agents'">Rapport complet</button></div><div class="help-row" v-for="stat in helpStatistics.filter(x => x.signaturesGiven || x.signaturesReceived).slice(0, 5)" :key="stat.agentId"><b>{{ stat.matricule }}</b><span><i :class="{ low: stat.signaturesGiven < stat.signaturesReceived }" :style="{ width: `${Math.min(100, 15 + stat.signaturesGiven * 12)}%` }"></i></span><small>{{ stat.signaturesGiven }} donnée(s) · {{ stat.signaturesReceived }} reçue(s)</small></div><p class="hint">Indicateur informatif uniquement — aucune sanction automatique.</p></article>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon"><AppIcon name="audit" /></span><strong>Activité récente</strong></div><button class="text-button" @click="adminTab = 'audit'">Tout voir</button></div><div class="activity" v-for="entry in adminAudit.slice(0, 5)" :key="entry.id"><span class="activity-dot"></span><div><b>{{ actionLabel(entry.action) }}</b><small>{{ entry.subjectMatricule || entityLabel(entry.entityType) }} · {{ new Date(entry.createdAt).toLocaleString('fr-BE') }}</small></div></div></article>
        </template>

        <template v-else-if="adminTab === 'agents'">
          <input v-model="agentFilter" class="admin-search" placeholder="Rechercher un matricule ou un statut" />
          <article class="admin-card agent-card" v-for="agent in filteredAgents()" :key="agent.id"><div><b>{{ agent.matricule }}</b><span class="status" :class="statusClass(agent.status)">{{ statusLabel(agent.status) }}</span><small>{{ agent.phoneNumber }}</small></div><div class="agent-actions" v-if="agent.role !== 'Admin'"><button v-if="agent.status !== 'Active'" class="approve" @click="changeAgentStatus(agent, 'Active')">Activer</button><button v-if="agent.status !== 'Suspended'" class="reject" @click="changeAgentStatus(agent, 'Suspended')">Suspendre</button><button v-if="agent.status === 'Pending'" class="reject" @click="changeAgentStatus(agent, 'Rejected')">Refuser</button></div></article>
          <article class="admin-card"><div class="card-title"><strong>Statistiques d’entraide</strong></div><div class="help-row" v-for="stat in helpStatistics" :key="stat.agentId"><b>{{ stat.matricule }}</b><span><i :class="{ low: stat.signaturesGiven < stat.signaturesReceived }" :style="{ width: `${Math.min(100, 10 + stat.signaturesGiven * 10)}%` }"></i></span><small>{{ stat.signaturesGiven }} donnée(s) · {{ stat.signaturesReceived }} reçue(s) · {{ stat.signatureOffers }} proposition(s)</small></div></article>
        </template>

        <template v-else-if="adminTab === 'exchanges'">
          <h2>Permutations ({{ adminPermutations.length }})</h2><article class="admin-card operation-card" v-for="item in adminPermutations" :key="item.id"><div><b>{{ item.requesterMatricule }}</b><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><div class="period-grid compact"><div class="period-card owned"><small>Période proposée</small><span class="period-range">{{ formatDate(item.ownedFrom) }} – {{ formatDate(item.ownedTo) }}</span></div><div class="period-card wanted"><small>Période souhaitée</small><span class="period-range">{{ formatDate(item.wantedFrom) }} – {{ formatDate(item.wantedTo) }}</span></div></div><small>{{ item.proposalCount }} proposition(s)</small></article>
          <h2>Signatures ({{ adminSignatures.length }})</h2><article class="admin-card operation-card" v-for="item in adminSignatures" :key="item.id"><div><b>{{ item.requesterMatricule }}</b><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><p>{{ formatDate(item.serviceDate) }}<span v-if="item.signerMatricule"> · signataire {{ item.signerMatricule }}</span></p><small>{{ item.offerCount }} proposition(s) · {{ item.comment || 'Sans commentaire' }}</small></article>
        </template>

        <template v-else>
          <article class="admin-card audit-card" v-for="entry in adminAudit" :key="entry.id"><div><b>{{ actionLabel(entry.action) }}</b><span>{{ entityLabel(entry.entityType) }}</span></div><p>Auteur : {{ entry.actorMatricule || 'Système' }}<br>Cible : {{ entry.subjectMatricule || entry.entityId }}</p><small>{{ new Date(entry.createdAt).toLocaleString('fr-BE') }}<span v-if="entry.reason"> · {{ entry.reason }}</span></small></article>
        </template>

        <nav class="admin-nav"><button :class="{ active: adminTab === 'home' }" @click="adminTab = 'home'"><AppIcon name="home" />Accueil</button><button :class="{ active: adminTab === 'agents' }" @click="adminTab = 'agents'"><AppIcon name="users" />Agents</button><button :class="{ active: adminTab === 'exchanges' }" @click="adminTab = 'exchanges'"><AppIcon name="permutation" />Échanges</button><button :class="{ active: adminTab === 'audit' }" @click="adminTab = 'audit'"><AppIcon name="audit" />Audit</button></nav>
      </section>
      <section v-else-if="view === 'home'" class="panel">
        <div class="home-title"><div><h1>Bonjour {{ session?.matricule }}</h1><p>Que veux-tu faire ?</p></div><button class="notification-button" :aria-label="notificationsExpanded ? 'Masquer les notifications' : 'Afficher les notifications'" :aria-expanded="notificationsExpanded" @click="toggleNotifications"><AppIcon name="bell" /><b v-if="notifications.some(n => !n.isRead)">{{ notifications.filter(n => !n.isRead).length }}</b></button></div>
        <div v-if="notificationsExpanded && notifications.length" class="notifications"><div class="notifications-title"><strong>Notifications</strong><div class="notification-actions"><button class="text-button" @click="showAllNotifications">Tout voir</button><button class="text-button" @click="markAllRead">Tout lire</button></div></div><button v-for="notification in notifications.slice(0, 5)" :key="notification.id" class="notification-item" :class="{ unread: !notification.isRead }" @click="openNotification(notification)"><span class="notification-kind"><AppIcon :name="notification.entityType === 'Permutation' ? 'permutation' : 'signature'" /></span><p>{{ notification.message }}<small>{{ new Date(notification.createdAt).toLocaleString('fr-BE') }}</small><em>Voir le détail</em></p></button></div>
        <nav class="category-grid">
          <button class="category-card" @click="view = 'permutation-menu'"><span class="category-icon"><AppIcon name="permutation" /></span><span><strong>Permutations</strong><small>Chercher ou suivre un échange de vacances</small></span><span class="category-cta">Ouvrir</span></button>
          <button class="category-card" @click="view = 'signature-menu'"><span class="category-icon"><AppIcon name="signature" /></span><span><strong>Signatures</strong><small>Demander, proposer ou suivre une signature</small></span><span class="category-cta">Ouvrir</span></button>
        </nav>
        <button class="secondary" @click="logout">Se déconnecter</button>
      </section>
      <section v-else-if="view === 'notifications'" class="stack notifications-page">
        <button class="section-back" @click="view = 'home'"><AppIcon name="home" /><span>Accueil</span></button>
        <div class="section-title"><h1>Toutes les notifications</h1><button v-if="notifications.some(item => !item.isRead)" class="text-button" @click="markAllRead">Tout marquer comme lu</button></div>
        <p v-if="!notifications.length" class="hint">Aucune notification.</p>
        <button v-for="notification in notifications" :key="notification.id" class="notification-item panel" :class="{ unread: !notification.isRead }" @click="openNotification(notification)"><span class="notification-kind"><AppIcon :name="notification.entityType === 'Permutation' ? 'permutation' : 'signature'" /></span><p>{{ notification.message }}<small>{{ new Date(notification.createdAt).toLocaleString('fr-BE') }}</small><em>Voir le détail</em></p></button>
      </section>
      <section v-else-if="view === 'permutation-menu'" class="panel menu-panel"><button class="back-link" @click="view = 'home'"><AppIcon name="home" /><span>Accueil</span></button><div class="menu-heading"><span class="menu-symbol"><AppIcon name="permutation" /></span><div><h1>Permutations</h1><p>Échanger mes périodes de vacances.</p></div></div><nav class="actions"><button class="action-tile" @click="view = 'new-permutation'"><AppIcon name="search" /><span><strong>Chercher une permutation</strong><small>Publier les dates à échanger</small></span></button><button class="action-tile" @click="loadPermutations"><AppIcon name="list" /><span><strong>Mes permutations</strong><small>Suivre mes demandes et réponses</small></span></button></nav></section>
      <section v-else-if="view === 'signature-menu'" class="panel menu-panel"><button class="back-link" @click="view = 'home'"><AppIcon name="home" /><span>Accueil</span></button><div class="menu-heading"><span class="menu-symbol"><AppIcon name="signature" /></span><div><h1>Signatures</h1><p>Demander ou proposer une aide.</p></div></div><nav class="actions"><button class="action-tile" @click="view = 'new-signature'"><AppIcon name="signature" /><span><strong>Demander une signature</strong><small>Publier une date</small></span></button><button class="action-tile" @click="loadMySignatures"><AppIcon name="list" /><span><strong>Mes signatures</strong><small>Suivre mes demandes et engagements</small></span></button><button class="action-tile" @click="loadAvailableSignatures"><AppIcon name="help" /><span><strong>Aider un collègue</strong><small>Voir les demandes en cours</small></span></button><button class="action-tile" @click="loadSignatureAvailabilities"><AppIcon name="calendar" /><span><strong>Mes disponibilités</strong><small>Proposer des jours à l’avance</small></span></button></nav></section>
      <section v-else-if="view === 'new-permutation'" class="panel"><button class="section-back" @click="view = 'permutation-menu'"><AppIcon name="permutation" /><span>Menu Permutations</span></button><h1>Nouvelle permutation</h1><form @submit.prevent="createPermutation"><fieldset class="owned-fieldset"><legend>Je propose</legend><label>Du<input v-model="ownedFrom" type="date" required /></label><label>Au<input v-model="ownedTo" type="date" required /></label></fieldset><fieldset class="wanted-fieldset"><legend>Je recherche</legend><label>Du<input v-model="wantedFrom" type="date" required /></label><label>Au<input v-model="wantedTo" type="date" required /></label></fieldset><p v-if="error" class="form-error" role="alert">{{ error }}</p><button :disabled="busy">Publier la demande</button></form></section>
      <section v-else-if="view === 'permutations'" class="stack"><button class="section-back" @click="view = 'permutation-menu'"><AppIcon name="permutation" /><span>Menu Permutations</span></button><article class="panel permutation-card" v-for="item in myPermutations" :id="`entity-${item.id}`" :key="item.id" :class="{ 'focused-entity': focusedEntityId === item.id }"><div class="card-heading"><h2>{{ item.requesterId === session?.id ? 'Ma demande' : 'Permutation proposée' }}</h2><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><div class="period-grid"><div class="period-card owned"><small>{{ periodLabel('owned') }}</small><span class="period-range">{{ formatDate(item.ownedPeriod.from) }} – {{ formatDate(item.ownedPeriod.to) }}</span></div><div class="period-card wanted"><small>{{ periodLabel('wanted') }}</small><span class="period-range">{{ formatDate(item.wantedPeriod.from) }} – {{ formatDate(item.wantedPeriod.to) }}</span></div></div><button v-if="currentAgentProposal(item)?.status === 'Pending'" class="waiting-action" disabled>Confirmation du collègue en attente</button><button v-for="proposal in item.proposals.filter(p => p.status === 'Pending' && item.requesterId === session?.id)" :key="proposal.id" @click="acceptProposal(item, proposal.id)">Accepter cette proposition</button><button v-if="item.status === 'Accepted' || item.status === 'Confirmed'" @click="confirmPermutation(item)">Confirmer l’échange</button><button v-if="item.requesterId === session?.id && !['Confirmed', 'Locked', 'Cancelled'].includes(item.status)" class="danger" @click="cancelPermutation(item)">Annuler ma demande</button></article><div class="section-title"><h2>Demandes disponibles</h2><small>{{ availablePermutations.length }} résultat(s)</small></div><article class="panel permutation-card" v-for="item in availablePermutations" :key="item.id"><div class="card-heading"><h2>Demande d’un collègue</h2><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><div class="period-grid"><div class="period-card owned"><small>Période proposée</small><span class="period-range">{{ formatDate(item.ownedPeriod.from) }} – {{ formatDate(item.ownedPeriod.to) }}</span></div><div class="period-card wanted"><small>Période souhaitée</small><span class="period-range">{{ formatDate(item.wantedPeriod.from) }} – {{ formatDate(item.wantedPeriod.to) }}</span></div></div><button @click="proposePermutation(item)">Proposer cette période</button></article></section>
      <section v-else-if="view === 'new-signature'" class="panel"><button class="section-back" @click="view = 'signature-menu'"><AppIcon name="signature" /><span>Menu Signatures</span></button><h1>Demander une signature</h1><form @submit.prevent="createSignature"><label>Date<input v-model="signatureDate" type="date" required /></label><label>Commentaire<textarea v-model="signatureComment" maxlength="500" /></label><p v-if="error" class="form-error" role="alert">{{ error }}</p><button :disabled="busy">Publier la demande</button></form></section>
      <section v-else-if="view === 'signatures'" class="stack">
        <button class="section-back" @click="view = 'signature-menu'"><AppIcon name="signature" /><span>Menu Signatures</span></button><h1>Mes signatures</h1>
        <p v-if="!mySignatures.length" class="hint">Tu n'as aucune demande de signature en cours.</p>
        <article class="panel" v-for="item in mySignatures" :id="`entity-${item.id}`" :key="item.id" :class="{ 'focused-entity': focusedEntityId === item.id }"><div class="card-heading"><h2>Signature du {{ formatDate(item.serviceDate) }}</h2><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><p>{{ item.comment || 'Sans commentaire' }}</p><p v-if="item.offers.some(o => o.status === 'Pending' && o.availabilityId)" class="match-badge">Collègue disponible à cette date.</p><button v-if="item.requesterId !== session?.id && currentAgentSignatureOffer(item)?.status === 'Pending'" class="waiting-action" disabled>Proposition envoyée</button><button v-for="offer in item.offers.filter(o => o.status === 'Pending' && item.requesterId === session?.id)" :key="offer.id" @click="confirmSigner(item, offer.id)">{{ offer.availabilityId ? 'Choisir ce collègue' : 'Choisir ce signataire' }}</button><button v-if="item.requesterId === session?.id && !['Locked', 'Cancelled'].includes(item.status)" class="danger" @click="cancelSignature(item)">Annuler ma demande</button></article>
      </section>
      <section v-else-if="view === 'available-signatures'" class="stack">
        <button class="section-back" @click="view = 'signature-menu'"><AppIcon name="signature" /><span>Menu Signatures</span></button><h1>Signatures recherchées</h1>
        <p v-if="!availableSignatures.length" class="hint">Aucun collègue ne recherche actuellement une signature que tu peux proposer.</p>
        <article class="panel" v-for="item in availableSignatures" :key="item.id"><div class="card-heading"><h2>{{ formatDate(item.serviceDate) }}</h2><span class="status" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span></div><p>{{ item.comment || 'Sans commentaire' }}</p><button @click="offerSignature(item)">Je peux signer</button></article>
      </section>
      <section v-else class="stack">
        <button class="section-back" @click="view = 'signature-menu'"><AppIcon name="signature" /><span>Menu Signatures</span></button>
        <article class="panel"><h1>Mes jours disponibles</h1><p>Propose à l'avance les jours où tu peux signer. Dès qu'un collègue demande une signature à la même date, vous êtes tous les deux avertis automatiquement.</p><form @submit.prevent="createSignatureAvailability"><label>Jour disponible<input v-model="availabilityDate" type="date" required /></label><label>Commentaire facultatif<textarea v-model="availabilityComment" maxlength="200" placeholder="Ex. Disponible en matinée" /></label><p v-if="error" class="form-error" role="alert">{{ error }}</p><button :disabled="busy">Proposer ce jour</button></form></article>
        <h2>Mes propositions</h2><p v-if="!mySignatureAvailabilities.length" class="hint">Tu n'as encore proposé aucun jour.</p>
        <article class="panel availability-card" v-for="item in mySignatureAvailabilities" :key="item.id"><span class="status" :class="{ inactive: !item.isActive }">{{ item.isActive ? 'Disponible' : 'Utilisée ou annulée' }}</span><h2>{{ formatDate(item.serviceDate) }}</h2><p>{{ item.comment || 'Sans commentaire' }}</p><button v-if="item.isActive" class="danger" @click="cancelSignatureAvailability(item)">Retirer cette disponibilité</button></article>
      </section>
    </template>
    <Teleport to="body">
      <div v-if="adminDecisionAgent" class="modal-backdrop" @click.self="closeAdminDecision">
        <section class="dialog-card" role="dialog" aria-modal="true" aria-labelledby="admin-decision-title">
          <h2 id="admin-decision-title">Décision pour {{ adminDecisionAgent.matricule }}</h2>
          <p>{{ statusLabel(adminDecisionStatus) }}</p>
          <form @submit.prevent="submitAgentDecision"><label>Motif<textarea v-model="adminDecisionReason" maxlength="500" required autofocus placeholder="Explique brièvement la décision" /></label><p v-if="error" class="form-error" role="alert">{{ error }}</p><div class="dialog-actions"><button type="button" class="secondary" @click="closeAdminDecision">Retour</button><button :disabled="busy">Confirmer</button></div></form>
        </section>
      </div>
    </Teleport>
    <Teleport to="body">
      <div v-if="confirmation" class="modal-backdrop" @click.self="confirmation = null">
        <section class="dialog-card" role="dialog" aria-modal="true" aria-labelledby="confirmation-title">
          <h2 id="confirmation-title">{{ confirmation.title }}</h2><p>{{ confirmation.message }}</p>
          <div class="dialog-actions"><button type="button" class="secondary" @click="confirmation = null">Garder</button><button type="button" class="danger-confirm" @click="runConfirmedAction">{{ confirmation.confirmLabel }}</button></div>
        </section>
      </div>
    </Teleport>
  </main>
</template>
