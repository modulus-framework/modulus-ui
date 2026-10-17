import type {ReactNode} from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import Heading from '@theme/Heading';

import styles from './index.module.css';

const cards = [
  {
    title: 'Getting started',
    to: '/docs/getting-started',
    text: 'Install the packages and show a first page in five minutes.',
  },
  {
    title: 'Tutorial: Blazor',
    to: '/docs/tutorial-blazor',
    text: 'Build a customer admin app step by step, with a table, a drawer form and a dashboard.',
  },
  {
    title: 'Tutorial: MVC',
    to: '/docs/tutorial-mvc',
    text: 'The same app on ASP.NET Core MVC, with htmx for live updates.',
  },
  {
    title: 'Components',
    to: '/docs/components',
    text: 'Every component by category, with its Blazor name and tag helper.',
  },
  {
    title: 'Pro components',
    to: '/docs/pro',
    text: 'Tables, editable and nested grids, pivot tables, dashboards and large data.',
  },
  {
    title: 'CLI',
    to: '/docs/cli',
    text: 'Scaffold pages, CRUD, the admin starter and themes from the command line.',
  },
];

function Hero(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <header className={clsx('hero', styles.hero)}>
      <div className="container">
        <Heading as="h1" className={styles.title}>
          {siteConfig.title}
        </Heading>
        <p className={styles.tagline}>{siteConfig.tagline}</p>
        <p className={styles.lead}>
          Build admin panels and business apps from ready-made components. Pick Blazor, ASP.NET Core MVC,
          Razor Pages or Liquid templates. The shell, theme, menu and components look and behave the same in each.
        </p>
        <div className={styles.buttons}>
          <Link className="button button--primary button--lg" to="/docs/getting-started">
            Get started
          </Link>
          <Link className="button button--secondary button--lg" to="/docs/tutorial-blazor">
            Build an app
          </Link>
        </div>
      </div>
    </header>
  );
}

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout title={siteConfig.title} description={siteConfig.tagline}>
      <Hero />
      <main className="container margin-vert--lg">
        <div className={styles.grid}>
          {cards.map((card) => (
            <Link key={card.to} to={card.to} className={styles.card}>
              <strong>{card.title}</strong>
              <span>{card.text}</span>
            </Link>
          ))}
        </div>
        <Heading as="h2">Choose a flavour</Heading>
        <table>
          <thead>
            <tr>
              <th>Flavour</th>
              <th>Choose it when</th>
              <th>Main package</th>
            </tr>
          </thead>
          <tbody>
            <tr>
              <td><strong>Blazor</strong></td>
              <td>You want components and state in C#, with live interaction and no hand-written JavaScript.</td>
              <td><code>Synvro.Modulus.Blazor</code></td>
            </tr>
            <tr>
              <td><strong>ASP.NET Core MVC</strong> or <strong>Razor Pages</strong></td>
              <td>You want server-rendered pages with tag helpers, and small live updates with htmx.</td>
              <td><code>Synvro.Modulus.AspNetCore.Mvc</code></td>
            </tr>
            <tr>
              <td><strong>Liquid</strong> templates</td>
              <td>You render pages or documents, such as invoices, from templates.</td>
              <td><code>Synvro.Modulus.AspNetCore.Fluid</code></td>
            </tr>
          </tbody>
        </table>
      </main>
    </Layout>
  );
}
