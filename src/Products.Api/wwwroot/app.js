// Grid produktów: lista ze stronicowaniem (skip/take), dodawanie i zmiana ceny (If-Match).
// Usuwanie produktów wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty.
// Dane trafiają do strony przez textContent (bez innerHTML), więc nazwy produktów nie są interpretowane jako HTML.
const api = '/api/products';
const pageSizes = [10, 20, 50, 100];
const priceFormat = new Intl.NumberFormat('pl-PL', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const dateFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short', timeStyle: 'medium' });

const $ = (id) => document.getElementById(id);
const state = { page: 1, take: 20, total: 0, highlight: null, selected: null };
const productDialog = $('product-dialog');
const form = productDialog.querySelector('form');
const field = (name) => form.elements.namedItem(name);
let submitForm = null;

function el(tag, props = {}, ...children) {
  const node = Object.assign(document.createElement(tag), props);
  node.append(...children);
  return node;
}

async function request(url, options) {
  const response = await fetch(url, options);
  const body = response.headers.get('content-type')?.includes('json') ? await response.json() : null;
  return { response, body };
}

const problemText = (body, response) => body?.detail ?? body?.title ?? `HTTP ${response.status}`;
const json = (method, body, headers = {}) =>
  ({ method, headers: { 'Content-Type': 'application/json', ...headers }, body: JSON.stringify(body) });

// --- lista i stronicowanie ---

const pageCount = () => Math.max(1, Math.ceil(state.total / state.take));

async function load() {
  const rows = $('rows');
  rows.classList.add('loading');
  try {
    const skip = (state.page - 1) * state.take;
    const { response, body } = await request(`${api}?skip=${skip}&take=${state.take}`);
    if (!response.ok)
      throw new Error(problemText(body, response));
    state.total = body.total;
    // strona za końcem listy (np. po usunięciu ostatniego produktu z ostatniej strony) - ostatnia istniejąca
    if (state.page > pageCount()) {
      state.page = pageCount();
      return await load();
    }
    rows.replaceChildren(...(body.items.length ? body.items.map(row) : [emptyRow()]));
    // zaznaczenie zostaje, jeśli produkt jest na wczytanej stronie (z aktualnymi danymi)
    select(body.items.find((p) => p.id === state.selected?.id) ?? null);
    state.highlight = null;
    renderPager();
    history.replaceState(null, '', `?page=${state.page}&size=${state.take}`);
  } catch (error) {
    toast(error.message, true);
  } finally {
    rows.classList.remove('loading');
  }
}

// Wiersz zaznacza kliknięcie albo Enter/spacja; dwuklik - edycja, strzałki - sąsiedni wiersz
function row(product) {
  const tr = el('tr', { tabIndex: 0, title: 'Kliknij, aby zaznaczyć; dwuklik - zmiana ceny' },
    el('td', { className: 'name' }, product.name, el('small', { textContent: product.id })),
    el('td', { className: 'num', textContent: priceFormat.format(product.price) }),
    el('td', { textContent: product.currency }),
    el('td', { textContent: dateFormat.format(new Date(product.updatedAtUtc)) }));
  tr.dataset.id = product.id;
  tr.addEventListener('click', () => select(product));
  tr.addEventListener('dblclick', () => editPrice(product.id));
  tr.addEventListener('keydown', (event) => {
    const next = { ArrowDown: tr.nextElementSibling, ArrowUp: tr.previousElementSibling }[event.key];
    if (next?.dataset.id) {
      next.focus();
      next.click();
    } else if (event.key === 'Enter' || event.key === ' ') {
      select(product);
    // usuwanie wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty
    // } else if (event.key === 'Delete') {
    //   select(product);
    //   remove(product);
    } else {
      return;
    }
    event.preventDefault();
  });
  if (product.id === state.highlight) {
    tr.classList.add('highlight');
    setTimeout(() => tr.classList.remove('highlight'), 1500);
  }
  return tr;
}

const emptyRow = () => el('tr', {}, el('td', { className: 'empty', colSpan: 4, textContent: 'Brak produktów.' }));

// Zaznaczony produkt - na nim działa przycisk edycji obok „Dodaj”
function select(product) {
  state.selected = product;
  for (const tr of $('rows').rows) {
    if (tr.dataset.id)
      tr.setAttribute('aria-selected', String(tr.dataset.id === product?.id));
  }
  $('edit').disabled = !product;
  // $('delete').disabled = !product; // usuwanie wyłączone (decyzja biznesowa)
}

function renderPager() {
  const pages = pageCount();
  const first = state.total === 0 ? 0 : (state.page - 1) * state.take + 1;
  const last = Math.min(state.page * state.take, state.total);
  $('range').textContent = `${first}–${last} z ${state.total.toLocaleString('pl-PL')}`;
  Object.assign($('go'), { max: pages, placeholder: `1–${pages}`, value: '' });

  // pierwsza, ostatnia i sąsiedztwo bieżącej strony, luki jako „…”
  const numbers = [...new Set([1, state.page - 2, state.page - 1, state.page, state.page + 1, state.page + 2, pages])]
    .filter((page) => page >= 1 && page <= pages)
    .sort((a, b) => a - b);
  const buttons = [navButton('«', 1, 'Pierwsza'), navButton('‹', state.page - 1, 'Poprzednia')];
  numbers.forEach((page, i) => {
    if (i > 0 && page - numbers[i - 1] > 1)
      buttons.push(el('span', { textContent: '…' }));
    const button = el('button', { textContent: page, title: `Strona ${page}`, onclick: () => goTo(page) });
    if (page === state.page)
      button.setAttribute('aria-current', 'page');
    buttons.push(button);
  });
  buttons.push(navButton('›', state.page + 1, 'Następna'), navButton('»', pages, 'Ostatnia'));
  $('pages').replaceChildren(...buttons);
}

const navButton = (label, page, title) => el('button', {
  textContent: label, title, onclick: () => goTo(page),
  disabled: page < 1 || page > pageCount() || page === state.page,
});

function goTo(page) {
  state.page = Math.min(Math.max(1, page), pageCount());
  load();
}

// --- dodawanie i zmiana ceny (wspólne okno) ---

// submit(values) zwraca ProblemDetails (okno zostaje otwarte z błędami) albo nic (okno się zamyka)
function openForm(title, info, values, withName, submit) {
  form.reset();
  showErrors(null);
  $('dialog-title').textContent = title;
  $('dialog-info').textContent = info;
  $('name-field').hidden = !withName;
  for (const [name, value] of Object.entries(values))
    field(name).value = value;
  submitForm = submit;
  productDialog.showModal();
}

form.addEventListener('submit', async (event) => {
  event.preventDefault();
  const save = form.querySelector('button.primary');
  const price = field('price').value;
  const values = { name: field('name').value, price: price === '' ? null : Number(price), currency: field('currency').value };
  save.disabled = true;
  showErrors(null);
  try {
    const problem = await submitForm(values);
    if (problem)
      showErrors(problem);
    else
      productDialog.close();
  } catch (error) {
    showErrors({ title: error.message });
  } finally {
    save.disabled = false;
  }
});
form.querySelector('button[type=button]').addEventListener('click', () => productDialog.close());

// błędy pól (ProblemDetails.errors) pod polami, pozostałe pod formularzem
function showErrors(problem) {
  form.querySelectorAll('[data-error]').forEach((node) => (node.textContent = ''));
  if (!problem)
    return;
  const other = problem.errors ? [] : [problem.detail ?? problem.title];
  for (const [name, messages] of Object.entries(problem.errors ?? {})) {
    const target = form.querySelector(`[data-error="${CSS.escape(name)}"]`);
    if (target)
      target.textContent = messages.join(' ');
    else
      other.push(...messages);
  }
  form.querySelector('[data-error=form]').textContent = other.join(' ');
}

// Idempotency-Key z otwarcia okna: ponowione wysłanie (np. podwójne kliknięcie, błąd sieci) nie tworzy drugiego produktu.
// crypto.randomUUID działa tylko w bezpiecznym kontekście (HTTPS, localhost) - poza nim losowe bajty.
const newKey = () => crypto.randomUUID?.()
  ?? Array.from(crypto.getRandomValues(new Uint8Array(16)), (b) => b.toString(16).padStart(2, '0')).join('');

function create() {
  const idempotencyKey = newKey();
  openForm('Nowy produkt', '', { currency: 'PLN' }, true, async (values) => {
    const { response, body } = await request(api, json('POST', values, { 'Idempotency-Key': idempotencyKey }));
    if (!response.ok)
      return body ?? { title: `HTTP ${response.status}` };
    toast(`Dodano: ${body.name}`);
    state.highlight = body.id;
    state.selected = body; // nowy produkt zaznaczony - od razu do edycji albo usunięcia
    state.page = 1; // lista od najnowszych
    await load();
  });
}

// Zmiana tylko wersji odczytanej przy otwarciu okna (If-Match = ETag z GET). Inna wersja (412): ktoś zmienił
// produkt w międzyczasie - okno pokazuje aktualne dane do ponownego zapisu.
async function editPrice(id) {
  const { response, body } = await request(`${api}/${id}`);
  if (!response.ok)
    return notFoundOrError(response, body);
  let etag = response.headers.get('ETag');
  const source = response.headers.get('X-Cache') === 'HIT' ? 'z cache (Redis)' : 'z bazy';
  openForm(`Zmiana ceny: ${body.name}`, `Wersja ${etag?.replaceAll('"', '')}, odczyt ${source}`,
    { price: body.price.toFixed(2), currency: body.currency }, false, async (values) => {
      const put = await request(`${api}/${id}/price`, json('PUT', values, { 'If-Match': etag }));
      if (put.response.status === 412) {
        const current = await request(`${api}/${id}`);
        if (!current.response.ok)
          return { title: problemText(current.body, current.response) };
        etag = current.response.headers.get('ETag');
        field('price').value = current.body.price.toFixed(2);
        field('currency').value = current.body.currency;
        $('dialog-info').textContent = `Wersja ${etag?.replaceAll('"', '')}, odczyt z bazy po konflikcie`;
        return { title: 'Ktoś zmienił produkt w międzyczasie. Pokazuję aktualne dane - zapisz ponownie.' };
      }
      if (!put.response.ok)
        return put.body ?? { title: `HTTP ${put.response.status}` };
      toast(put.body.message ? 'Cena i waluta bez zmian - nic nie zapisano.' : `Zmieniono cenę: ${put.body.name}`);
      state.highlight = id;
      await load();
    });
}

// --- usuwanie: wyłączone (decyzja biznesowa) - kod zakomentowany, nie usunięty ---

// function confirmDelete(name) {
//   const dialog = $('delete-dialog');
//   $('delete-name').textContent = name;
//   dialog.returnValue = '';
//   dialog.showModal();
//   return new Promise((resolve) => dialog.addEventListener('close', () => resolve(dialog.returnValue === 'delete'), { once: true }));
// }
//
// async function remove(product) {
//   if (!await confirmDelete(product.name))
//     return;
//   const { response, body } = await request(`${api}/${product.id}`, { method: 'DELETE' });
//   if (!response.ok)
//     return notFoundOrError(response, body);
//   toast(`Usunięto: ${product.name}`);
//   await load();
// }

async function notFoundOrError(response, body) {
  if (response.status !== 404)
    return toast(problemText(body, response), true);
  toast('Produkt już nie istnieje.', true);
  await load();
}

// --- reszta ---

let toastTimer;
function toast(message, error = false) {
  Object.assign($('toast'), { textContent: message, className: error ? 'show error' : 'show' });
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => ($('toast').className = ''), 4000);
}

// motyw: przycisk przełącza jasny/ciemny i zapamiętuje wybór (index.html stosuje go przed renderem); bez wyboru -
// motyw systemu. Ikona pokazuje motyw, na który przełącza.
const darkSystem = matchMedia('(prefers-color-scheme: dark)');
const currentTheme = () => document.documentElement.dataset.theme ?? (darkSystem.matches ? 'dark' : 'light');

function renderThemeButton() {
  const dark = currentTheme() === 'dark';
  $('sun').toggleAttribute('hidden', !dark);
  $('moon').toggleAttribute('hidden', dark);
  $('theme').title = dark ? 'Włącz tryb jasny' : 'Włącz tryb ciemny';
}

function toggleTheme() {
  const theme = currentTheme() === 'dark' ? 'light' : 'dark';
  document.documentElement.dataset.theme = theme;
  try {
    localStorage.setItem('theme', theme);
  } catch {
    // bez zapamiętania (np. zablokowany localStorage) - motyw do odświeżenia strony
  }
  renderThemeButton();
}

async function showVariant() {
  try {
    const { body } = await request('/health/ready');
    $('variant').textContent = body?.variant ?? '';
  } catch {
    // bez wariantu w nagłówku - grid działa dalej
  }
}

const params = new URLSearchParams(location.search);
state.page = Math.max(1, Number.parseInt(params.get('page'), 10) || 1);
state.take = pageSizes.includes(Number(params.get('size'))) ? Number(params.get('size')) : state.take;
$('take').value = state.take;
$('take').addEventListener('change', () => {
  const firstItem = (state.page - 1) * state.take; // ta sama pozycja listy po zmianie rozmiaru strony
  state.take = Number($('take').value);
  state.page = Math.floor(firstItem / state.take) + 1;
  load();
});
$('go').addEventListener('change', () => goTo(Number($('go').value)));
$('add').addEventListener('click', create);
$('edit').addEventListener('click', () => editPrice(state.selected.id));
// $('delete').addEventListener('click', () => remove(state.selected)); // usuwanie wyłączone (decyzja biznesowa)
$('refresh').addEventListener('click', load);
$('theme').addEventListener('click', toggleTheme);
darkSystem.addEventListener('change', renderThemeButton);

renderThemeButton();

showVariant();
load();
