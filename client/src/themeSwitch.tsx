import { useEffect, useState } from "react";
import { Form } from "react-bootstrap";

const theme = localStorage.getItem('theme');

export default function ThemeSwitch() {

    const [dark, setDark] = useState(theme === 'dark');

    useEffect(() => {
        const theme = dark ? 'dark' : 'light';
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem('theme', theme);
    }, [dark]);

    return (
        <Form.Switch
            checked={dark}
            label={(dark ? 'Enable Light Mode' : 'Enable Dark Mode')}
            onChange={e => setDark(e.target.checked)}>
        </Form.Switch>
    );
}