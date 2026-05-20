# Refresh Tokens Guide

## Wat zijn Refresh Tokens?

Refresh tokens zijn langdurige tokens die gebruikt worden om nieuwe access tokens (JWT) te verkrijgen zonder opnieuw in te loggen. Dit verbetert zowel de beveiliging als de admin-ervaring.

## Hoe werkt het?

1. **Login**: Admin ontvangt een JWT access token (60 min) + refresh token (7 dagen)
2. **Access token verloopt**: Na 60 minuten is de JWT access token verlopen
3. **Refresh**: Frontend gebruikt de refresh token om een nieuwe access token te krijgen
4. **Automatisch**: Admin blijft ingelogd zonder opnieuw credentials in te voeren

Admin accounts worden aangemaakt door bestaande admins of via seeding; bezoekers hebben geen account nodig.

## Beveiliging Features

? **Secure Token Generatie**: 64-byte cryptografisch random tokens
? **Token Rotatie**: Oude refresh token wordt ingetrokken bij gebruik
? **Expiratie**: Refresh tokens verlopen na 7 dagen (configureerbaar)
? **Revocation**: Tokens kunnen worden ingetrokken
? **Single Use**: Elke refresh token kan maar 1x gebruikt worden
? **Logout All Devices**: Alle refresh tokens van een admin account kunnen worden ingetrokken

## API Endpoints

### 1. Login (Krijgt Access + Refresh Token)
```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "admin@example.com",
  "password": "Test123!"
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "kYj3L8Xm9QwErTy...",
  "email": "admin@example.com",
  "roles": ["Admin"],
  "expiresAt": "2024-01-17T13:37:08Z"
}
```

### 2. Refresh Token (Verkrijg nieuwe tokens)
```http
POST /api/auth/refresh-token
Content-Type: application/json

{
  "refreshToken": "kYj3L8Xm9QwErTy..."
}
```

**Response:**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",  // Nieuwe access token
  "refreshToken": "p7K2m5N8vRxWqZt...",  // Nieuwe refresh token
  "email": "admin@example.com",
  "roles": ["Admin"],
  "expiresAt": "2024-01-17T14:37:08Z"
}
```

### 3. Revoke Token (Intrekken van specifieke token)
```http
POST /api/auth/revoke-token
Authorization: Bearer {access-token}
Content-Type: application/json

{
  "refreshToken": "kYj3L8Xm9QwErTy..."
}
```

**Response:**
```json
{
  "message": "Token revoked successfully"
}
```

### 4. Logout All Devices (Alle tokens intrekken)
```http
POST /api/auth/logout-all
Authorization: Bearer {access-token}
```

**Response:**
```json
{
  "message": "Logged out from all devices successfully"
}
```

## Frontend Implementatie

### JavaScript/TypeScript

```javascript
class AuthService {
  constructor() {
    this.accessToken = localStorage.getItem('accessToken');
    this.refreshToken = localStorage.getItem('refreshToken');
  }

  async login(email, password) {
    const response = await fetch('https://localhost:7xxx/api/auth/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password })
    });

    const data = await response.json();
    this.saveTokens(data.token, data.refreshToken);
    return data;
  }

  saveTokens(accessToken, refreshToken) {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);
    this.accessToken = accessToken;
    this.refreshToken = refreshToken;
  }

  async refreshAccessToken() {
    try {
      const response = await fetch('https://localhost:7xxx/api/auth/refresh-token', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: this.refreshToken })
      });

      if (!response.ok) {
        // Refresh token is invalid/expired - logout
        this.logout();
        throw new Error('Session expired');
      }

      const data = await response.json();
      this.saveTokens(data.token, data.refreshToken);
      return data.token;
    } catch (error) {
      this.logout();
      throw error;
    }
  }

  async apiRequest(url, options = {}) {
    // Voeg access token toe aan request
    options.headers = {
      ...options.headers,
      'Authorization': `Bearer ${this.accessToken}`
    };

    let response = await fetch(url, options);

    // Als 401 Unauthorized, probeer token te refreshen
    if (response.status === 401) {
      try {
        await this.refreshAccessToken();
        // Retry original request met nieuwe token
        options.headers['Authorization'] = `Bearer ${this.accessToken}`;
        response = await fetch(url, options);
      } catch (error) {
        // Redirect naar login
        window.location.href = '/login';
        throw error;
      }
    }

    return response;
  }

  logout() {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    this.accessToken = null;
    this.refreshToken = null;
  }

  async logoutAllDevices() {
    await this.apiRequest('https://localhost:7xxx/api/auth/logout-all', {
      method: 'POST'
    });
    this.logout();
  }
}

// Gebruik
const auth = new AuthService();

// Login
await auth.login('admin@example.com', 'Test123!');

// API call met automatische token refresh
const response = await auth.apiRequest('https://localhost:7xxx/api/test/admin');
const data = await response.json();
```

### React met Axios Interceptor

```javascript
import axios from 'axios';

const api = axios.create({
  baseURL: 'https://localhost:7xxx',
  withCredentials: true
});

// Request interceptor - voeg access token toe
api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('accessToken');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  error => Promise.reject(error)
);

// Response interceptor - refresh token bij 401
api.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config;

    // Als 401 en nog niet eerder geprobeerd te refreshen
    if (error.response?.status === 401 && !originalRequest._retry) {
      originalRequest._retry = true;

      try {
        const refreshToken = localStorage.getItem('refreshToken');
        const response = await axios.post(
          'https://localhost:7xxx/api/auth/refresh-token',
          { refreshToken }
        );

        const { token, refreshToken: newRefreshToken } = response.data;
        
        // Sla nieuwe tokens op
        localStorage.setItem('accessToken', token);
        localStorage.setItem('refreshToken', newRefreshToken);

        // Retry original request
        originalRequest.headers.Authorization = `Bearer ${token}`;
        return api(originalRequest);
      } catch (refreshError) {
        // Refresh failed - logout
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        window.location.href = '/login';
        return Promise.reject(refreshError);
      }
    }

    return Promise.reject(error);
  }
);

export default api;
```

### Vue 3 Composable

```javascript
import { ref } from 'vue';
import axios from 'axios';

const accessToken = ref(localStorage.getItem('accessToken'));
const refreshToken = ref(localStorage.getItem('refreshToken'));

export function useAuth() {
  const saveTokens = (access, refresh) => {
    accessToken.value = access;
    refreshToken.value = refresh;
    localStorage.setItem('accessToken', access);
    localStorage.setItem('refreshToken', refresh);
  };

  const refreshAccessToken = async () => {
    try {
      const response = await axios.post('/api/auth/refresh-token', {
        refreshToken: refreshToken.value
      });
      
      saveTokens(response.data.token, response.data.refreshToken);
      return response.data.token;
    } catch (error) {
      logout();
      throw error;
    }
  };

  const login = async (email, password) => {
    const response = await axios.post('/api/auth/login', { email, password });
    saveTokens(response.data.token, response.data.refreshToken);
    return response.data;
  };

  const logout = () => {
    accessToken.value = null;
    refreshToken.value = null;
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
  };

  return {
    accessToken,
    refreshToken,
    login,
    logout,
    refreshAccessToken,
    saveTokens
  };
}
```

## Configuratie

In `appsettings.json`:

```json
{
  "JwtSettings": {
    "ExpiryInMinutes": "60",           // Access token: 60 minuten
    "RefreshTokenExpiryInDays": "7"    // Refresh token: 7 dagen
  }
}
```

**Aanbevolen waarden:**
- **Development**: Access 60 min, Refresh 7 dagen
- **Production**: Access 15 min, Refresh 30 dagen

## Database

De `RefreshTokens` tabel slaat op:
- `Token`: Unique refresh token string
- `UserId`: Gekoppelde admin account
- `ExpiresAt`: Expiratie datum
- `CreatedAt`: Aanmaak datum
- `RevokedAt`: Intrekking datum (null = actief)
- `ReplacedByToken`: Referentie naar nieuwe token
- `ReasonRevoked`: Reden van intrekking

## Best Practices

### 1. Token Opslag
```javascript
// ? GOED: localStorage voor web apps
localStorage.setItem('refreshToken', token);

// ? GOED: Secure httpOnly cookie (vereist backend aanpassing)
// Backend: res.cookie('refreshToken', token, { httpOnly: true, secure: true });

// ? FOUT: Nooit in normale variabelen (verloren bij refresh)
let refreshToken = token; // Verdwijnt bij page refresh
```

### 2. Automatische Refresh
```javascript
// Refresh token kort voor expiratie
setInterval(async () => {
  const expiresAt = localStorage.getItem('tokenExpiresAt');
  const now = Date.now();
  const fiveMinutes = 5 * 60 * 1000;
  
  if (expiresAt && (expiresAt - now) < fiveMinutes) {
    await refreshAccessToken();
  }
}, 60000); // Check elke minuut
```

### 3. Logout Implementatie
```javascript
async function logout() {
  const refreshToken = localStorage.getItem('refreshToken');
  
  // Revoke refresh token op server
  if (refreshToken) {
    await fetch('/api/auth/revoke-token', {
      method: 'POST',
      headers: { 
        'Authorization': `Bearer ${accessToken}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ refreshToken })
    });
  }
  
  // Clear local storage
  localStorage.clear();
  window.location.href = '/login';
}
```

## Security Considerations

?? **Belangrijk:**
- Refresh tokens zijn gevoelig - behandel als wachtwoorden
- Gebruik HTTPS in productie
- Implementeer rate limiting op refresh endpoint
- Monitor voor verdachte refresh activiteit
- Overweeg httpOnly cookies voor refresh tokens (XSS bescherming)

## Troubleshooting

### "Invalid or expired refresh token"
- Token is verlopen (>7 dagen oud)
- Token is al gebruikt (replaced)
- Token is handmatig ingetrokken
- **Oplossing**: Gebruiker moet opnieuw inloggen

### Refresh loop (constant refreshen)
- Check of nieuwe tokens correct worden opgeslagen
- Verificeer expiratie tijd configuratie
- Check browser console voor errors

### 401 na refresh
- Access token wordt niet correct bijgewerkt in request headers
- Timing issue - wacht op refresh completion
