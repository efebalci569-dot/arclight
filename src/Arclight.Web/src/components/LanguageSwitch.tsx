import { languages, useLanguage } from '../services/language';

/** EN | TR toggle in the header. Switching re-renders the page in place; the choice is remembered. */
export function LanguageSwitch() {
  const { language, setLanguage, t } = useLanguage();
  return (
    <div className="lang-switch" role="group" aria-label={t.layout.language}>
      {languages.map((option) => (
        <button
          key={option.id}
          type="button"
          className="lang-switch__option"
          lang={option.id}
          title={option.name}
          aria-label={option.name}
          aria-pressed={option.id === language}
          onClick={() => setLanguage(option.id)}
        >
          {option.short}
        </button>
      ))}
    </div>
  );
}
