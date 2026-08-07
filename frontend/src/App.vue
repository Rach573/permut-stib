<script setup lang="ts">
import { ref } from 'vue'
import { api } from './api'

type View = 'login' | 'register' | 'pending' | 'home'

const view = ref<View>('login')
const busy = ref(false)
const error = ref('')

const identifier = ref('')
const loginPassword = ref('')

const matricule = ref('')
const phoneNumber = ref('')
const password = ref('')
const passwordConfirmation = ref('')

async function login() {
  error.value = ''
  busy.value = true
  try {
    await api.login({ identifier: identifier.value, password: loginPassword.value })
    view.value = 'home'
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Connexion impossible.'
  } finally {
    busy.value = false
  }
}

async function register() {
  error.value = ''
  if (password.value !== passwordConfirmation.value) {
    error.value = 'Les mots de passe ne correspondent pas.'
    return
  }

  busy.value = true
  try {
    await api.register({ matricule: matricule.value, phoneNumber: phoneNumber.value, password: password.value })
    view.value = 'pending'
  } catch (exception) {
    error.value = exception instanceof Error ? exception.message : 'Inscription impossible.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="shell">
    <header>
      <span class="mark">↔</span>
      <div><strong>Permut' STIB</strong><small>Entraide entre agents</small></div>
    </header>

    <section v-if="view === 'login'" class="panel">
      <h1>Se connecter</h1>
      <p>Utilise ton matricule STIB ou ton numéro de GSM.</p>
      <form @submit.prevent="login">
        <label>Matricule ou GSM<input v-model="identifier" autocomplete="username" required /></label>
        <label>Mot de passe<input v-model="loginPassword" type="password" autocomplete="current-password" required /></label>
        <p v-if="error" class="error">{{ error }}</p>
        <button :disabled="busy">Se connecter</button>
      </form>
      <button class="secondary" @click="view = 'register'">Créer mon compte</button>
    </section>

    <section v-else-if="view === 'register'" class="panel">
      <h1>Créer mon compte</h1>
      <p>Le délégué devra valider ton inscription.</p>
      <form @submit.prevent="register">
        <label>Matricule STIB<input v-model="matricule" required /></label>
        <label>GSM<input v-model="phoneNumber" type="tel" autocomplete="tel" required /></label>
        <label>Mot de passe<input v-model="password" type="password" autocomplete="new-password" required /></label>
        <label>Confirmer<input v-model="passwordConfirmation" type="password" autocomplete="new-password" required /></label>
        <p v-if="error" class="error">{{ error }}</p>
        <button :disabled="busy">Envoyer ma demande</button>
      </form>
      <button class="secondary" @click="view = 'login'">Retour</button>
    </section>

    <section v-else-if="view === 'pending'" class="panel">
      <h1>Demande envoyée</h1>
      <p>Ton compte est en attente de validation par le délégué.</p>
      <button @click="view = 'login'">Retour à la connexion</button>
    </section>

    <section v-else class="panel">
      <h1>Bienvenue</h1>
      <p>Le socle sécurisé est prêt. Les modules Permutations et Signatures seront branchés ici.</p>
    </section>
  </main>
</template>

