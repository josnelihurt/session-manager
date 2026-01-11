import { Navbar } from './Navbar'
import './AdminLayout.css'

export function AdminLayout({ children, title }) {
  return (
    <div className="admin-layout">
      <Navbar />
      <main className="admin-content">
        {title && <h1 className="page-title">{title}</h1>}
        {children}
      </main>
    </div>
  )
}
