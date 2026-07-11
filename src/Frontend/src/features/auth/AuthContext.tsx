import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { api, clearSession, loadSession, saveSession, type Session } from "../../shared/api";
import type { AuthResponse } from "../../shared/types";

interface AuthContextValue {
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  register: (email: string, displayName: string, password: string) => Promise<void>;
  logout: () => void;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(loadSession);

  const login = useCallback(async (email: string, password: string) => {
    const auth = await api.post<AuthResponse>("/api/auth/login", { email, password });
    setSession(saveSession(auth));
  }, []);

  const register = useCallback(async (email: string, displayName: string, password: string) => {
    const auth = await api.post<AuthResponse>("/api/auth/register", { email, displayName, password });
    setSession(saveSession(auth));
  }, []);

  const logout = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  return <AuthContext.Provider value={{ session, login, register, logout }}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (!value) throw new Error("useAuth は AuthProvider の内側で使用してください。");
  return value;
}
