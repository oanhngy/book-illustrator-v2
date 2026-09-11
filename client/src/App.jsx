import { useEffect, useState } from 'react'
import './App.css'

function App() {
  const [health, setHealth]=useState(null)
  const [error, setError]=useState(null)

  useEffect(() => {
    fetch('http://localhost:5050/api/health')
      .then((res)=>res.json()) //parse body -->object Json
      .then((data)=>setHealth(data)) //lưu vô state
      .catch((err)=>setError(err.message)) //catch lỗi, k để app crash trắng
  },[])

  return (
    <div>
      {error && <p style={{color:'red'}}>{error}</p>} {/* ưu tiên hiển thị lỗi nếu có */}
      {!error && !health && <p>Loading...</p>} {/* chờ fetch */}
      {health && <pre>{JSON.stringify(health, null, 2)}</pre>} 
    </div>
  )
}

export default App