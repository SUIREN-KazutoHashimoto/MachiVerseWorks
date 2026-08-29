import * as THREE from 'three';

import './style.css';

const app = document.querySelector<HTMLDivElement>('#app');

if (app === null) {
  throw new Error('Application root was not found.');
}

const scene = new THREE.Scene();
scene.background = new THREE.Color(0x0b1020);

const camera = new THREE.PerspectiveCamera(60, 1, 0.1, 1000);
camera.position.z = 5;

const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
app.append(renderer.domElement);

const identity = document.createElement('div');
identity.className = 'identity';
identity.innerHTML = `<strong>MachiVerseWorks</strong><span>v${__APP_VERSION__}</span>`;
app.append(identity);

const resize = (): void => {
  const width = window.innerWidth;
  const height = window.innerHeight;

  camera.aspect = width / Math.max(height, 1);
  camera.updateProjectionMatrix();
  renderer.setSize(width, height, false);
};

window.addEventListener('resize', resize);
resize();

renderer.setAnimationLoop(() => {
  renderer.render(scene, camera);
});
