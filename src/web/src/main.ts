import { Application } from './application.ts';
import './style.css';

const appRoot = document.querySelector<HTMLDivElement>('#app');
if (appRoot === null) {
  throw new Error('Application root was not found.');
}

const identity = document.createElement('div');
identity.className = 'identity';
const name = document.createElement('strong');
name.textContent = 'MachiVerseWorks';
const version = document.createElement('span');
version.textContent = `v${__APP_VERSION__}`;
identity.append(name, version);
appRoot.append(identity);

const application = new Application(appRoot);
application.start();
window.addEventListener('beforeunload', () => application.dispose(), { once: true });
