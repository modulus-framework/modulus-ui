// Modulus UI Interop — theme, utilities

export function initTheme(isDark) {
  if (isDark) {
    document.documentElement.setAttribute('data-theme', 'dark');
    document.body.classList.add('ant-theme-dark');
  } else {
    document.documentElement.removeAttribute('data-theme');
    document.body.classList.remove('ant-theme-dark');
  }
}

export function toggleTheme(isDark) {
  initTheme(isDark);
  window.dispatchEvent(new CustomEvent('modulusThemeChanged', { detail: { isDark } }));
}

export function setLocale(locale) {
  document.documentElement.lang = locale.split('-')[0];
}

export function getPrefersDark() {
  return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
}
