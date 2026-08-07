<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api, type Permutation, type Session, type Signature } from './api'

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

async function execute(action: () => Promise<void>) {
  error.value = ''; busy.value = true
  try { await action() } catch (exception) { error.value = exception instanceof Error ? exception.message : 'Action impossible.' }
  finally { busy.value = false }
}

async function login() { await execute(async () => { session.value = await api.login({ identifier: identifier.value, password: loginPassword.value }) as Session; view.value = 'home' }) }
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
async function loadSignatures() { await execute(async () => { [mySignatures.value, availableSignatures.value] = await Promise.all([api.mySignatures(), api.availableSignatures()]); view.value = 'signatures' }) }
async function createSignature() { await execute(async () => { await api.createSignature(signatureDate.value, signatureComment.value); await loadSignatures() }) }
async function offerSignature(item: Signature) { await execute(async () => { await api.offerSignature(item.id); await loadSignatures() }) }
async function confirmSigner(item: Signature, offerId: string) { await execute(async () => { await api.confirmSigner(item.id, offerId); await loadSignatures() }) }

onMounted(async () => {
  if (import.meta.env.DEV && new URLSearchParams(location.search).get('preview') === 'admin') {
    session.value = { id: 'preview', matricule: 'DÉLÉGUÉ', role: 'Admin' }
    view.value = 'home'
    return
  }
  try { session.value = await api.me(); view.value = 'home' } catch { /* aucune session */ }
})
</script>

<template>
  <main class="shell">
    <header><span class="mark">↔</span><div><strong>Permut' STIB</strong><small>Entraide entre agents</small></div></header>
    <p v-if="error" class="error notice">{{ error }}</p>

    <section v-if="view === 'login'" class="panel">
      <h1>Se connecter</h1><p>Utilise ton matricule STIB ou ton GSM.</p>
      <form @submit.prevent="login"><label>Matricule ou GSM<input v-model="identifier" required /></label><label>Mot de passe<input v-model="loginPassword" type="password" required /></label><button :disabled="busy">Se connecter</button></form>
      <button class="secondary" @click="view = 'register'">Créer mon compte</button>
    </section>
    <section v-else-if="view === 'register'" class="panel">
      <h1>Créer mon compte</h1><p>Le délégué validera ton inscription.</p>
      <form @submit.prevent="register"><label>Matricule<input v-model="matricule" required /></label><label>GSM<input v-model="phoneNumber" required /></label><label>Mot de passe<input v-model="password" type="password" required /></label><label>Confirmer<input v-model="passwordConfirmation" type="password" required /></label><button :disabled="busy">Envoyer</button></form>
      <button class="secondary" @click="view = 'login'">Retour</button>
    </section>
    <section v-else-if="view === 'pending'" class="panel"><h1>Demande envoyée</h1><p>Ton compte attend la validation du délégué.</p><button @click="view = 'login'">Retour</button></section>

    <template v-else>
      <section v-if="view === 'home' && session?.role === 'Admin'" class="admin-dashboard">
        <div class="admin-heading"><div><span class="eyebrow">ESPACE DÉLÉGUÉ</span><h1>Vue d’ensemble</h1><p>Les éléments qui demandent ton attention.</p></div><button class="icon-button" title="Se déconnecter" @click="logout">↪</button></div>
        <div class="metric-grid"><article class="metric urgent"><strong>4</strong><span>Inscriptions<br>en attente</span></article><article class="metric"><strong>12</strong><span>Permutations<br>ouvertes</span></article><article class="metric"><strong>7</strong><span>Signatures<br>recherchées</span></article><article class="metric success"><strong>26</strong><span>Opérations<br>confirmées</span></article></div>
        <article class="admin-card"><div class="card-title"><div><span class="card-icon">👤</span><strong>Inscriptions à valider</strong></div><button class="text-button">Voir les 4</button></div><div class="approval"><div><b>70-445</b><small>0466 ••• •• 63 · aujourd’hui</small></div><div class="inline-actions"><button class="approve">✓</button><button class="reject">×</button></div></div><div class="approval"><div><b>71-208</b><small>0471 ••• •• 18 · hier</small></div><div class="inline-actions"><button class="approve">✓</button><button class="reject">×</button></div></div></article>
        <article class="admin-card"><div class="card-title"><div><span class="card-icon">↔</span><strong>Activité récente</strong></div><button class="text-button">Tout voir</button></div><div class="activity"><span class="activity-dot locked"></span><div><b>Permutation verrouillée</b><small>70-312 ↔ 72-105 · il y a 18 min</small></div></div><div class="activity"><span class="activity-dot signed"></span><div><b>Signature confirmée</b><small>17 juillet · il y a 42 min</small></div></div><div class="activity"><span class="activity-dot waiting"></span><div><b>Nouvelle proposition</b><small>Vacances août → juillet · il y a 1 h</small></div></div></article>
        <article class="admin-card"><div class="card-title"><div><span class="card-icon">⚖</span><strong>Entraide</strong></div><button class="text-button">Rapport</button></div><div class="help-row"><b>70-446</b><span><i style="width:78%"></i></span><small>7 données · 2 reçues</small></div><div class="help-row"><b>70-445</b><span><i class="low" style="width:18%"></i></span><small>1 donnée · 8 reçues</small></div><p class="hint">Indicateur informatif uniquement — aucune sanction automatique.</p></article>
        <nav class="admin-nav"><button><span>⌂</span>Accueil</button><button><span>👥</span>Agents</button><button><span>↔</span>Échanges</button><button><span>☷</span>Audit</button></nav>
      </section>
      <section v-else-if="view === 'home'" class="panel">
        <h1>Bonjour {{ session?.matricule }}</h1><p>Que veux-tu faire ?</p>
        <nav class="actions"><button @click="view = 'new-permutation'">Chercher une permutation</button><button @click="loadPermutations">Mes permutations</button><button @click="view = 'new-signature'">Demander une signature</button><button @click="loadSignatures">Signatures recherchées</button></nav>
        <button class="secondary" @click="logout">Se déconnecter</button>
      </section>
      <section v-else-if="view === 'new-permutation'" class="panel"><h1>Nouvelle permutation</h1><form @submit.prevent="createPermutation"><fieldset><legend>Je possède</legend><label>Du<input v-model="ownedFrom" type="date" required /></label><label>Au<input v-model="ownedTo" type="date" required /></label></fieldset><fieldset><legend>Je recherche</legend><label>Du<input v-model="wantedFrom" type="date" required /></label><label>Au<input v-model="wantedTo" type="date" required /></label></fieldset><button :disabled="busy">Publier</button></form><button class="secondary" @click="view = 'home'">Retour</button></section>
      <section v-else-if="view === 'permutations'" class="stack"><button class="secondary" @click="view = 'home'">← Accueil</button><article class="panel" v-for="item in myPermutations" :key="item.id"><span class="status">{{ item.status }}</span><h2>Ma demande</h2><p>Je possède {{ item.ownedPeriod.from }} → {{ item.ownedPeriod.to }}<br>Je recherche {{ item.wantedPeriod.from }} → {{ item.wantedPeriod.to }}</p><button v-for="proposal in item.proposals.filter(p => p.status === 'Pending')" :key="proposal.id" @click="acceptProposal(item, proposal.id)">Accepter cette proposition</button><button v-if="item.status === 'Accepted' || item.status === 'Confirmed'" @click="confirmPermutation(item)">Confirmer définitivement</button></article><h2>Demandes disponibles</h2><article class="panel" v-for="item in availablePermutations" :key="item.id"><span class="status">{{ item.status }}</span><p>Recherche {{ item.wantedPeriod.from }} → {{ item.wantedPeriod.to }}</p><button @click="proposePermutation(item)">Proposer cette période</button></article></section>
      <section v-else-if="view === 'new-signature'" class="panel"><h1>Demander une signature</h1><form @submit.prevent="createSignature"><label>Date<input v-model="signatureDate" type="date" required /></label><label>Commentaire<textarea v-model="signatureComment" maxlength="500" /></label><button :disabled="busy">Publier</button></form><button class="secondary" @click="view = 'home'">Retour</button></section>
      <section v-else class="stack"><button class="secondary" @click="view = 'home'">← Accueil</button><article class="panel" v-for="item in mySignatures" :key="item.id"><span class="status">{{ item.status }}</span><h2>{{ item.serviceDate }}</h2><p>{{ item.comment }}</p><button v-for="offer in item.offers.filter(o => o.status === 'Pending')" :key="offer.id" @click="confirmSigner(item, offer.id)">Choisir ce signataire</button></article><h2>Demandes disponibles</h2><article class="panel" v-for="item in availableSignatures" :key="item.id"><span class="status">{{ item.status }}</span><h2>{{ item.serviceDate }}</h2><p>{{ item.comment }}</p><button @click="offerSignature(item)">Je peux signer</button></article></section>
    </template>
  </main>
</template>
