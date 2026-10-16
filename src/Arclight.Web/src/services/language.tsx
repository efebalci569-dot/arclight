import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { en, type Messages } from '../i18n/en';
import { tr } from '../i18n/tr';

export type Language = 'en' | 'tr';

/** The header switch. Each name is written in its own language. */
export const languages: { id: Language; short: string; name: string }[] = [
  { id: 'en', short: 'EN', name: 'English' },
  { id: 'tr', short: 'TR', name: 'Türkçe' },
];

const messages: Record<Language, Messages> = { en, tr };
const locales: Record<Language, string> = { en: 'en', tr: 'tr-TR' };
const storageKey = 'arclight.language';

/** A choice made with the switch wins; otherwise follow the browser, as the app follows Windows. */
function initialLanguage(): Language {
  try {
    const saved = localStorage.getItem(storageKey);
    if (saved === 'en' || saved === 'tr') {
      return saved;
    }
  } catch {
    // Storage can be unavailable (private mode); fall back to the browser language.
  }

  return navigator.language.toLowerCase().startsWith('tr') ? 'tr' : 'en';
}

interface LanguageState {
  language: Language;
  /** BCP 47 tag for dates and numbers. */
  locale: string;
  t: Messages;
  setLanguage: (language: Language) => void;
}

const LanguageContext = createContext<LanguageState | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [language, setLanguageState] = useState(initialLanguage);

  useEffect(() => {
    document.documentElement.lang = language;
  }, [language]);

  const setLanguage = useCallback((next: Language) => {
    setLanguageState(next);
    try {
      localStorage.setItem(storageKey, next);
    } catch {
      // Ignore storage failures; the choice still applies to this visit.
    }
  }, []);

  const value = useMemo(() => ({ language, locale: locales[language], t: messages[language], setLanguage }), [language, setLanguage]);
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export function useLanguage(): LanguageState {
  const state = useContext(LanguageContext);
  if (!state) {
    throw new Error('useLanguage must be used inside LanguageProvider.');
  }

  return state;
}
