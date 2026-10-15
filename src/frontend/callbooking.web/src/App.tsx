import { BrowserRouter, Link, Route, Routes } from 'react-router-dom'
import type { ApiClient } from './api/client'
import BookingPage from './BookingPage'
import UpcomingBookingsPage from './UpcomingBookingsPage'
import './App.css'

function HomePage() {
  return (
    <section className="hero" aria-labelledby="calendar-title">
      <div className="hero-description">
        <p className="eyebrow">БЫСТРАЯ ЗАПИСЬ НА ЗВОНОК</p>
        <h1 id="calendar-title">Calendar</h1>
        <p>Один экран, понятные слоты, быстрая бронь. Выберите время и запишитесь на звонок без лишних шагов.</p>
        <Link className="primary-action" to="/booking">
          Записаться
          <svg viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" focusable="false">
            <path d="M5 12h14M13 6l6 6-6 6" />
          </svg>
        </Link>
      </div>
      <aside className="features" aria-labelledby="features-title">
        <h2 id="features-title">Что доступно прямо сейчас</h2>
        <ul>
          <li>Длительность звонка зависит от выбранного типа события.</li>
          <li>Проверка конфликта при бронировании.</li>
          <li>Просмотр предстоящих событий в отдельном разделе.</li>
        </ul>
      </aside>
    </section>
  )
}

function App({ client }: { client?: ApiClient }) {
  return (
    <BrowserRouter>
      <header className="site-header">
        <div className="container header-content">
          <Link className="brand" to="/">
            <svg viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true" focusable="false">
              <rect x="3" y="5" width="18" height="16" rx="2" />
              <path d="M16 3v4M8 3v4M3 11h18M8 15h2M14 15h2" />
            </svg>
            Calendar
          </Link>
          <nav aria-label="Основная навигация">
            <Link to="/booking">Записаться</Link>
            <Link to="/events">Предстоящие события</Link>
          </nav>
        </div>
      </header>
      <main className="container">
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/booking" element={<BookingPage client={client} />} />
          <Route path="/events" element={<UpcomingBookingsPage client={client} />} />
        </Routes>
      </main>
    </BrowserRouter>
  )
}

export default App
