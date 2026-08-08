<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { api, type AdminAgent, type AdminAuditEntry, type AdminPermutation, type AdminSignature, type AdminSummary, type AgentNotification, type HelpStatistics, type Permutation, type Session, type Signature } from './api'

type View = 'login' | 'register' | 'pending' | 'home' | 'new-permutation' | 'permutations' | 'new-signature' | 'signatures'
const view = ref<View>('login')
const busy = ref(false)
const error = ref('')
const session = ref<Session | null>(null)
const identifier = ref(''), loginPassword = ref('')
const matricule = ref(''), phoneNumber = ref(''), password = ref(''), passwordConfirmation = ref('')
const ownedFrom = ref(''), ownedTo = ref(''), wantedFrom = ref(''), wantedTo = ref('')
const signatureDate = ref(''), signatureComment = ref('')
const myPermutations = ref<Permutation[]>([]), availablePermutations = ref<Permutation[]>([])
const mySignatures = ref<Signature[]>([]), availableSignatures = ref<Signature[]>([])
const notifications = ref<AgentNotification[]>([])
const adminSummary = ref<AdminSummary | null>(null)
const adminAgents = ref<AdminAgent[]>([])
const adminPermutations = ref<AdminPermutation[]>([])
const adminSignatures = ref<AdminSignature[]>([])
const helpStatistics = ref<HelpStatistics[]>([])
const adminAudit = ref<AdminAuditEntry[]>([])
const adminTab = ref<'home' | 'agents' | 'exchanges' | 'audit'>('home')
const agentFilter = ref('')
const splashVisible = ref(true)
let restoringHistory = false

async function execute(action: () => Promise<void>) {
  error.value = ''; busy.value = true
  try { await action() } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Action impossible.' }
  finally { busy.value = false }
}

async function login() { await execute(async () => { session.value = await api.login({ identifier: identifier.value, password: loginPassword.value }) as Session; view.value = 'home'; if (session.value.role === 'Admin') await loadAdminData(); else notifications.value = await api.notifications() }) }
async function register() {
  if (password.value !== passwordConfirmation.value) { error.value = 'Les mots de passe ne correspondent pas.'; return }
  await execute(async () => { await api.register({ matricule: matricule.value, phoneNumber: phoneNumber.value, password: password.value }); view.value = 'pending' })
}
async function logout() { await execute(async () => { await api.logout(); session.value = null; view.value = 'login' }) }
async function loadPermutations() { await execute(async () => { [myPermutations.value, availablePermutations.value] = await Promise.all([api.myPermutations(), api.availablePermutations()]); view.value = 'permutations' }) }
async function createPermutation() { await execute(async () => { await api.createPermutation({ from: ownedFrom.value, to: ownedTo.value }, { from: wantedFrom.value, to: wantedTo.value }); await loadPermutations() }) }
async function proposePermutation(item: Permutation) { await execute(async () => { await api.proposePermutation(item.id, item.wantedPeriod); await loadPermutations() }) }
async function acceptProposal(item: Permutation, proposalId: string) { await execute(async () => { await api.acceptProposal(item.id, proposalId); await loadPermutations() }) }
async function confirmPermutation(item: Permutation) { await execute(async () => { await api.confirmPermutation(item.id); await loadPermutations() }) }
async function cancelPermutation(item: Permutation) { await execute(async () => { await api.cancelPermutation(item.id); await loadPermutations() }) }
async function loadSignatures() { await execute(async () => { [mySignatures.value, availableSignatures.value] = await Promise.all([api.mySignatures(), api.availableSignatures()]); view.value = 'signatures' }) }
async function createSignature() { await execute(async () => { await api.createSignature(signatureDate.value, signatureComment.value); await loadSignatures() }) }
async function offerSignature(item: Signature) { await execute(async () => { await api.offerSignature(item.id); await loadSignatures() }) }
async function confirmSigner(item: Signature, offerId: string) { await execute(async () => { await api.confirmSigner(item.id, offerId); await loadSignatures() }) }
async function cancelSignature(item: Signature) { await execute(async () => { await api.cancelSignature(item.id); await loadSignatures() }) }
async function loadNotifications() { await execute(async () => { notifications.value = await api.notifications() }) }
async function markAllRead() { await execute(async () => { await api.markAllNotificationsRead(); await loadNotifications() }) }

async function loadAdminData() {
  [adminSummary.value, adminAgents.value, adminPermutations.value, adminSignatures.value, helpStatistics.value, adminAudit.value] = await Promise.all([
    api.adminSummary(), api.adminAgents(), api.adminPermutations(), api.adminSignatures(), api.helpStatistics(), api.adminAudit(),
  ])
}

async function changeAgentStatus(agent: AdminAgent, status: AdminAgent['status']) {
  const labels = { Active: 'activation', Suspended: 'suspension', Rejected: 'refus', Pending: 'mise en attente' }
  const reason = window.prompt(`Motif de ${labels[status]} pour ${agent.matricule} :`, 'Décision du délégué — démonstration')
  if (reason === null) return
  await execute(async () => { await api.setAgentStatus(agent.id, status, reason); await loadAdminData() })
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
  if (!restoringHistory) history.pushState({ view: nextView, adminTab: nextAdminTab }, '')
})

onMounted(async () => {
  history.replaceState({ view: view.value, adminTab: adminTab.value }, '')
  window.addEventListener('popstate', restoreFromHistory)
  window.setTimeout(() => { splashVisible.value = false }, 900)
  if (import.meta.env.DEV && new URLSearchParams(location.search).get('preview') === 'admin') {
    session.value = { id: 'preview', matricule: 'DÉLÉGUÉ', role: 'Admin' }
    view.value = 'home'
    return
  }
  try { session.value = await api.me(); view.value = 'home'; if (session.value.role === 'Admin') await loadAdminData(); else notifications.value = await api.notifications() } catch { /* aucune session */ }
})

onBeforeUnmount(() => window.removeEventListener('popstate', restoreFromHistory))
</script>

<template>
  <main class="shell">
    <Transition name="splash-fade"><div v-if="splashVisible" class="splash-screen"><img src="/logo-csc.png" alt="Logo CSC" /><strong>Permut' STIB</strong><small>L’entraide entre collègues</small></div></Transition>
    <header><span class="mark">↔</span><div><strong>Permut' STIB</strong><small>Entraide entre agents</small></div></header>
    <p v-if="error" class="error notice">{{ error }}</p>

    <section v-if="view === 'login'" class="panel">
      <h1>Se connecter</h1><p>Utilise ton matricule STIB ou ton GSM.</p>
      <form @submit.prevent="login"><label>Matricule ou GSM<input v-model="identifier" placeholder="Ex. 70-001 ou 0470 00 00 01" required /></label><label>Mot de passe<input v-model="loginPassword" type="password" placeholder="Votre mot de passe" required /></label><button :disabled="busy">Se connecter</button></form>
      <button class="secondary" @click="view = 'register'">Créer mon compte</button>
    </section>
    <section v-else-if="view === 'register'" class="panel">
      <h1>Créer mon compte</h1><p>Après l’inscription, le délégué doit activer le compte avant la première connexion.</p>
      <form @submit.prevent="register"><label>Matricule<input v-model="matricule" placeholder="Ex. 70-123" minlength="3" required /></label><label>GSM belge<input v-model="phoneNumber" type="tel" placeholder="Ex. 0470 00 00 01" required /></label><label>Mot de passe<input v-model="password" type="password" minlength="8" placeholder="8 caractères minimum" required /></label><small class="form-help">Pour la démonstration, vous pouvez utiliser <b>test1234</b>.</small><label>Confirmer<input v-model="passwordConfirmation" type="password" minlength="8" required /></label><button :disabled="busy">Envoyer l’inscription</button></form>
      <button class="secondary" @click="view = 'login'">Retour</button>
    </section>
    <section v-else-if="view === 'pending'" class="panel"><span class="pending-icon">✓</span><h1>Inscription enregistrée</h1><p>Le compte est maintenant <b>en attente</b>. Connectez-vous avec <b>DELEGUE</b>, ouvrez « Agents », puis appuyez sur « Activer ». L’agent pourra ensuite se connecter.</p><button @click="view = 'login'">Retour à la connexion</button></section>

    <template v-else>
      <section v-if="view === 'home' && session?.role === 'Admin'" class="admin-dashboard">
        <div class="admin-heading"><div><span class="eyebrow">ESPACE DÉLÉGUÉ</span><h1>{{ adminTab === 'home' ? 'Vue d’ensemble' : adminTab === 'agents' ? 'Agents' : adminTab === 'exchanges' ? 'Échanges' : 'Journal d’audit' }}</h1><p>Données réelles de la base de démonstration.</p></div><button class="icon-button" title="Se déconnecter" @click="logout">↪</button></div>

        <template v-if="adminTab === 'home' && adminSummary">
          <div class="metric-grid"><article class="metric urgent"><strong>{{ adminSummary.pendingAgents }}</strong><span>Inscriptions<br>en attente</span></article><article class="metric"><strong>{{ adminSummary.openPermutations }}</strong><span>Permutations<br>ouvertes</span></article><article class="metric"><strong>{{ adminSummary.openSignatures }}</strong><span>Signatures<br>recherchées</span></article><article class="metric success"><strong>{{ adminSummary.confirmedPermutations + adminSummary.confirmedSignatures }}</strong><span>Opérations<br>confirmées</span></article></div>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon">👤</span><strong>Inscriptions à valider</strong></div><button class="text-button" @click="adminTab = 'agents'">Tous les agents</button></div><p v-if="!adminAgents.some(x => x.status === 'Pending')" class="hint">Aucune inscription en attente.</p><div class="approval" v-for="agent in adminAgents.filter(x => x.status === 'Pending').slice(0, 5)" :key="agent.id"><div><b>{{ agent.matricule }}</b><small>{{ agent.phoneNumber }} · {{ new Date(agent.createdAt).toLocaleDateString('fr-BE') }}</small></div><div class="inline-actions"><button class="approve" title="Valider" @click="changeAgentStatus(agent, 'Active')">✓</button><button class="reject" title="Refuser" @click="changeAgentStatus(agent, 'Rejected')">×</button></div></div></article>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon">⚖</span><strong>Entraide</strong></div><button class="text-button" @click="adminTab = 'agents'">Rapport complet</button></div><div class="help-row" v-for="stat in helpStatistics.filter(x => x.signaturesGiven || x.signaturesReceived).slice(0, 5)" :key="stat.agentId"><b>{{ stat.matricule }}</b><span><i :class="{ low: stat.signaturesGiven < stat.signaturesReceived }" :style="{ width: `${Math.min(100, 15 + stat.signaturesGiven * 12)}%` }"></i></span><small>{{ stat.signaturesGiven }} donnée(s) · {{ stat.signaturesReceived }} reçue(s)</small></div><p class="hint">Indicateur informatif uniquement — aucune sanction automatique.</p></article>
          <article class="admin-card"><div class="card-title"><div><span class="card-icon">☷</span><strong>Activité récente</strong></div><button class="text-button" @click="adminTab = 'audit'">Tout voir</button></div><div class="activity" v-for="entry in adminAudit.slice(0, 5)" :key="entry.id"><span class="activity-dot"></span><div><b>{{ entry.action }}</b><small>{{ entry.subjectMatricule || entry.entityType }} · {{ new Date(entry.createdAt).toLocaleString('fr-BE') }}</small></div></div></article>
        </template>

        <template v-else-if="adminTab === 'agents'">
          <input v-model="agentFilter" class="admin-search" placeholder="Rechercher un matricule ou un statut" />
          <article class="admin-card agent-card" v-for="agent in filteredAgents()" :key="agent.id"><div><b>{{ agent.matricule }}</b><span class="status">{{ agent.status }}</span><small>{{ agent.phoneNumber }}</small></div><div class="agent-actions" v-if="agent.role !== 'Admin'"><button v-if="agent.status !== 'Active'" class="approve" @click="changeAgentStatus(agent, 'Active')">Activer</button><button v-if="agent.status !== 'Suspended'" class="reject" @click="changeAgentStatus(agent, 'Suspended')">Suspendre</button><button v-if="agent.status === 'Pending'" class="reject" @click="changeAgentStatus(agent, 'Rejected')">Refuser</button></div></article>
          <article class="admin-card"><div class="card-title"><strong>Statistiques d’entraide</strong></div><div class="help-row" v-for="stat in helpStatistics" :key="stat.agentId"><b>{{ stat.matricule }}</b><span><i :class="{ low: stat.signaturesGiven < stat.signaturesReceived }" :style="{ width: `${Math.min(100, 10 + stat.signaturesGiven * 10)}%` }"></i></span><small>{{ stat.signaturesGiven }} donnée(s) · {{ stat.signaturesReceived }} reçue(s) · {{ stat.signatureOffers }} proposition(s)</small></div></article>
        </template>

        <template v-else-if="adminTab === 'exchanges'">
          <h2>Permutations ({{ adminPermutations.length }})</h2><article class="admin-card operation-card" v-for="item in adminPermutations" :key="item.id"><div><b>{{ item.requesterMatricule }}</b><span class="status">{{ item.status }}</span></div><p>Possède {{ item.ownedFrom }} → {{ item.ownedTo }}<br>Recherche {{ item.wantedFrom }} → {{ item.wantedTo }}</p><small>{{ item.proposalCount }} proposition(s)</small></article>
          <h2>Signatures ({{ adminSignatures.length }})</h2><article class="admin-card operation-card" v-for="item in adminSignatures" :key="item.id"><div><b>{{ item.requesterMatricule }}</b><span class="status">{{ item.status }}</span></div><p>{{ item.serviceDate }}<span v-if="item.signerMatricule"> · signataire {{ item.signerMatricule }}</span></p><small>{{ item.offerCount }} proposition(s) · {{ item.comment || 'Sans commentaire' }}</small></article>
        </template>

        <template v-else>
          <article class="admin-card audit-card" v-for="entry in adminAudit" :key="entry.id"><div><b>{{ entry.action }}</b><span>{{ entry.entityType }}</span></div><p>{{ entry.actorMatricule || 'Système' }} → {{ entry.subjectMatricule || entry.entityId }}</p><small>{{ new Date(entry.createdAt).toLocaleString('fr-BE') }}<span v-if="entry.reason"> · {{ entry.reason }}</span></small></article>
        </template>

        <nav class="admin-nav"><button :class="{ active: adminTab === 'home' }" @click="adminTab = 'home'"><span>⌂</span>Accueil</button><button :class="{ active: adminTab === 'agents' }" @click="adminTab = 'agents'"><span>👥</span>Agents</button><button :class="{ active: adminTab === 'exchanges' }" @click="adminTab = 'exchanges'"><span>↔</span>Échanges</button><button :class="{ active: adminTab === 'audit' }" @click="adminTab = 'audit'"><span>☷</span>Audit</button></nav>
      </section>
      <section v-else-if="view === 'home'" class="panel">
        <div class="home-title"><div><h1>Bonjour {{ session?.matricule }}</h1><p>Que veux-tu faire ?</p></div><button class="notification-button" @click="loadNotifications">🔔<b v-if="notifications.some(n => !n.isRead)">{{ notifications.filter(n => !n.isRead).length }}</b></button></div>
        <div v-if="notifications.length" class="notifications"><div class="notifications-title"><strong>Notifications</strong><button class="text-button" @click="markAllRead">Tout marquer comme lu</button></div><article v-for="notification in notifications.slice(0, 5)" :key="notification.id" :class="{ unread: !notification.isRead }"><span></span><p>{{ notification.message }}<small>{{ new Date(notification.createdAt).toLocaleString('fr-BE') }}</small></p></article></div>
        <nav class="actions"><button @click="view = 'new-permutation'">Chercher une permutation</button><button @click="loadPermutations">Mes permutations</button><button @click="view = 'new-signature'">Demander une signature</button><button @click="loadSignatures">Signatures recherchées</button></nav>
        <button class="secondary" @click="logout">Se déconnecter</button>
      </section>
      <section v-else-if="view === 'new-permutation'" class="panel"><h1>Nouvelle permutation</h1><form @submit.prevent="createPermutation"><fieldset><legend>Je possède</legend><label>Du<input v-model="ownedFrom" type="date" required /></label><label>Au<input v-model="ownedTo" type="date" required /></label></fieldset><fieldset><legend>Je recherche</legend><label>Du<input v-model="wantedFrom" type="date" required /></label><label>Au<input v-model="wantedTo" type="date" required /></label></fieldset><button :disabled="busy">Publier</button></form><button class="secondary" @click="view = 'home'">Retour</button></section>
      <section v-else-if="view === 'permutations'" class="stack"><button class="secondary" @click="view = 'home'">← Accueil</button><article class="panel" v-for="item in myPermutations" :key="item.id"><span class="status">{{ item.status }}</span><h2>{{ item.requesterId === session?.id ? 'Ma demande' : 'Permutation proposée' }}</h2><p>Possédée {{ item.ownedPeriod.from }} → {{ item.ownedPeriod.to }}<br>Recherchée {{ item.wantedPeriod.from }} → {{ item.wantedPeriod.to }}</p><button v-for="proposal in item.proposals.filter(p => p.status === 'Pending' && item.requesterId === session?.id)" :key="proposal.id" @click="acceptProposal(item, proposal.id)">Accepter cette proposition</button><button v-if="item.status === 'Accepted' || item.status === 'Confirmed'" @click="confirmPermutation(item)">Confirmer définitivement</button><button v-if="item.requesterId === session?.id && !['Confirmed', 'Locked', 'Cancelled'].includes(item.status)" class="danger" @click="cancelPermutation(item)">Annuler ma demande</button></article><h2>Demandes disponibles</h2><article class="panel" v-for="item in availablePermutations" :key="item.id"><span class="status">{{ item.status }}</span><p>Recherche {{ item.wantedPeriod.from }} → {{ item.wantedPeriod.to }}</p><button @click="proposePermutation(item)">Proposer cette période</button></article></section>
      <section v-else-if="view === 'new-signature'" class="panel"><h1>Demander une signature</h1><form @submit.prevent="createSignature"><label>Date<input v-model="signatureDate" type="date" required /></label><label>Commentaire<textarea v-model="signatureComment" maxlength="500" /></label><button :disabled="busy">Publier</button></form><button class="secondary" @click="view = 'home'">Retour</button></section>
      <section v-else class="stack"><button class="secondary" @click="view = 'home'">← Accueil</button><article class="panel" v-for="item in mySignatures" :key="item.id"><span class="status">{{ item.status }}</span><h2>{{ item.serviceDate }}</h2><p>{{ item.comment }}</p><button v-for="offer in item.offers.filter(o => o.status === 'Pending' && item.requesterId === session?.id)" :key="offer.id" @click="confirmSigner(item, offer.id)">Choisir ce signataire</button><button v-if="item.requesterId === session?.id && !['Locked', 'Cancelled'].includes(item.status)" class="danger" @click="cancelSignature(item)">Annuler ma demande</button></article><h2>Demandes disponibles</h2><article class="panel" v-for="item in availableSignatures" :key="item.id"><span class="status">{{ item.status }}</span><h2>{{ item.serviceDate }}</h2><p>{{ item.comment }}</p><button @click="offerSignature(item)">Je peux signer</button></article></section>
    </template>
  </main>
</template>
