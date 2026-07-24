import { createContext, useContext, useState } from 'react';
import type { ReactNode } from 'react';
import { jwtDecode } from 'jwt-decode';

interface JwtClaims {
  unique_name?: string;
  nameid?: string;
  sub?: string;
  role?: string | string[];
  exp?: number;
}

export interface AuthUser {
  id: string;
  name: string;
  roles: string[];
}

interface AuthContextType {
  user: AuthUser | null;
  isAuthenticated: boolean;
  hasRole: (...roles: string[]) => boolean;
  login: (accessToken: string, refreshToken: string) => void;
  logout: () => void;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

const REFRESH_WINDOW_MS = 7 * 24 * 60 * 60 * 1000;

const parseUser = (token: string): AuthUser | null => {
  try {
    const claims = jwtDecode<JwtClaims>(token);

    if (claims.exp && claims.exp * 1000 + REFRESH_WINDOW_MS < Date.now()) {
      return null;
    }

    const roles = claims.role == null ? [] : Array.isArray(claims.role) ? claims.role : [claims.role];

    return {
      id: claims.nameid ?? claims.sub ?? '',
      name: claims.unique_name ?? 'User',
      roles
    };
  }
  catch {
    return null;
  }
};

export const AuthProvider = ({ children }: { children: ReactNode }) => {
  const [user, setUser] = useState<AuthUser | null>(() => {
    const token = localStorage.getItem('accessToken');
    
    if (!token) {
      return null;
    }

    const parsed = parseUser(token);
    
    if (!parsed) {
      localStorage.removeItem('accessToken');
      localStorage.removeItem('refreshToken');
    }

    return parsed;
  });

  const login = (accessToken: string, refreshToken: string) => {
    localStorage.setItem('accessToken', accessToken);
    localStorage.setItem('refreshToken', refreshToken);

    setUser(parseUser(accessToken));
  };

  const logout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');

    setUser(null);
  };

  const hasRole = (...roles: string[]) => !!user && roles.some(role => user.roles.includes(role));

  return (
    <AuthContext.Provider value={{ user, isAuthenticated: !!user, hasRole, login, logout }}>
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = (): AuthContextType => {
  const context = useContext(AuthContext);
  
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }

  return context;
};
