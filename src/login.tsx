import { useState } from 'react';
import './App.css';
import { Alert, Button, Form } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

const api = process.env.REACT_APP_API_PATH;

export interface ILoginProps {
    readonly finishLogin: (name: string) => void;
}
export function Login(props: ILoginProps) {

    const name = localStorage.getItem('name');
    const [mainName, setMainName] = useState(name || '');
    const login = async () => {
        localStorage.setItem('name', mainName);
        await axios.post(api + "/login", { mainName });
        props.finishLogin(mainName);
    };

    return (
        <Alert variant={'primary'}>
            <Alert.Heading>Let's Get Started!</Alert.Heading>
            <p>Please enter your <strong>MAIN</strong> character name (<em>not</em> your alt/box or "Mickey Mouse")</p>
            <Form onSubmit={login}>
                <Form.Control type="text" value={mainName} onChange={e => setMainName(e.target.value.trim())} />
            </Form>
            <br />
            <Button onClick={login} disabled={mainName === ''} variant="primary">Start</Button>
        </Alert>
    );
}