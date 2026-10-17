import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

// The site is served from GitHub Pages at https://modulus-framework.github.io/modulus-ui/.
// For Cloudflare Pages or a custom domain, set DOCS_BASE_URL (for example "/") and DOCS_URL when building.
const url = process.env.DOCS_URL ?? 'https://modulus-framework.github.io';
const baseUrl = process.env.DOCS_BASE_URL ?? '/modulus-ui/';

const config: Config = {
  title: 'Modulus UI',
  tagline: 'Admin UI components for Blazor, ASP.NET Core MVC, Razor Pages and Liquid templates',
  favicon: 'img/icon.png',

  url,
  baseUrl,

  organizationName: 'modulus-framework',
  projectName: 'modulus-ui',
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      {
        hashed: true,
        language: ['en'],
        docsRouteBasePath: '/docs',
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
        indexBlog: false,
      },
    ],
  ],

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
          routeBasePath: 'docs',
          editUrl: 'https://github.com/modulus-framework/modulus-ui/tree/main/website/',
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  themeConfig: {
    image: 'img/social-card.png',
    imageAlt: 'Modulus UI, admin components for Blazor, MVC, Razor Pages and Liquid',
    colorMode: {
      respectPrefersColorScheme: true,
    },
    metadata: [
      { name: 'description', content: 'Modulus UI: build admin panels and business apps from ready-made components for Blazor, ASP.NET Core MVC, Razor Pages and Liquid templates.' },
      { name: 'keywords', content: 'Blazor, ASP.NET Core, MVC, Razor Pages, Ant Design, admin template, components, .NET 10' },
      { property: 'og:type', content: 'website' },
      { name: 'twitter:card', content: 'summary_large_image' },
    ],
    navbar: {
      title: 'Modulus UI',
      logo: {
        alt: 'Modulus UI logo',
        src: 'img/logo.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'docsSidebar',
          position: 'left',
          label: 'Docs',
        },
        {
          to: '/docs/getting-started',
          label: 'Getting started',
          position: 'left',
        },
        {
          to: '/docs/tutorial-blazor',
          label: 'Tutorial',
          position: 'left',
        },
        {
          href: 'https://www.nuget.org/packages?q=Synvro.Modulus',
          label: 'NuGet',
          position: 'right',
        },
        {
          href: 'https://github.com/modulus-framework/modulus-ui',
          label: 'GitHub',
          position: 'right',
        },
      ],
    },
    footer: {
      style: 'dark',
      links: [
        {
          title: 'Documentation',
          items: [
            { label: 'Getting started', to: '/docs/getting-started' },
            { label: 'Build with Blazor', to: '/docs/tutorial-blazor' },
            { label: 'Build with MVC', to: '/docs/tutorial-mvc' },
            { label: 'Components', to: '/docs/components' },
            { label: 'CLI', to: '/docs/cli' },
          ],
        },
        {
          title: 'Community',
          items: [
            { label: 'GitHub', href: 'https://github.com/modulus-framework/modulus-ui' },
            { label: 'Issues', href: 'https://github.com/modulus-framework/modulus-ui/issues' },
          ],
        },
        {
          title: 'More',
          items: [
            { label: 'NuGet packages', href: 'https://www.nuget.org/packages?q=Synvro.Modulus' },
            { label: 'Samples', href: 'https://github.com/modulus-framework/modulus-ui/tree/main/docs/examples' },
          ],
        },
      ],
      copyright: `Copyright ${new Date().getFullYear()} Modulus. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
      additionalLanguages: ['csharp', 'bash', 'json'],
    },
  } satisfies Preset.ThemeConfig,
};

export default config;
