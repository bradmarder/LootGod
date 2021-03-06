import { useEffect, useState } from 'react';
import './App.css';
import { Container, Row, Col, Alert, Button } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { Login } from './login';
import { LootRequests } from './lootRequests';
import { CreateLootRequest } from './createLootRequest';
import { CreateLoot } from './createLoot';
import Loots from './loots';
import { GrantedLoots } from './grantedLoots';

const api = process.env.REACT_APP_API_PATH;
const name = localStorage.getItem('name');

export default function App() {
    const [loading, setLoading] = useState(false);
    const [isReady, setIsReady] = useState(name != null);
    const [lootLock, setLootLock] = useState(false);
    const [mainName, setMainName] = useState(name || '');
    const [requests, setRequests] = useState<ILootRequest[]>([]);
    const [loots, setLoots] = useState<ILoot[]>([]);

    const getLoots = async () => {
        const res = await axios.get<ILoot[]>(api + '/GetLoots');
        setLoots(res.data);
    };
    const getLootRequests = async () => {
        const res = await axios.get<ILootRequest[]>(api + '/GetLootRequests');
        setRequests(res.data);
    };

    const isAdmin = ['Benemage', 'Vhau', 'Lainea'].includes(mainName);

    const getLootLock = async () => {
        const res = await axios.get<boolean>(api + '/GetLootLock');
        setLootLock(res.data);
    };
    const enableLootLock = async () => {
        setLoading(true);
        try {
            await axios.post(api + '/EnableLootLock');
        }
        finally {
            setLoading(false);
        }
    };
    const disableLootLock = async () => {
        setLoading(true);
        try {
            await axios.post(api + '/DisableLootLock');
        }
        finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        (async () => {
            await getLootLock();
            await getLoots();
            await getLootRequests();
        })();
    }, []);

    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl(api + "/lootHub")
            .build();

        connection.on("refresh", (lootLock: boolean, loots: ILoot[], requests: ILootRequest[]) => {
            setLootLock(lootLock);
            setLoots(loots);
            setRequests(requests);
        });

        connection.start();
    }, []);

    const logout = () => {
        localStorage.removeItem('name');
        setMainName('');
        setIsReady(false);
    };
    const finishLogin = (name: string) => {
        setMainName(name);
        setIsReady(true);
    };

    return (
        <Container fluid>
            <Row>
                <Col xs={12} md={8}>
                    <h1>KOI Raid Loot Tool</h1>
                    {!isReady &&
                        <Login finishLogin={finishLogin} />
                    }
                    {isReady &&
                        <Row>
                            <Col>
                                <CreateLootRequest requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></CreateLootRequest>
                                <br />
                                <LootRequests requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></LootRequests>
                                <br />
                                {isAdmin &&
                                    <GrantedLoots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} lootLocked={lootLock}></GrantedLoots>
                                }
                            </Col>
                            <Col>
                                {isAdmin &&
                                    <>
                                        <CreateLoot></CreateLoot>
                                        {lootLock &&
                                            <Button variant={'success'} onClick={disableLootLock} disabled={loading}>Unlock/Enable Loot Requests</Button>
                                        }
                                        {!lootLock &&
                                            <Button variant={'danger'} onClick={enableLootLock} disabled={loading}>Lock/Disable Loot Requests</Button>
                                        }
                                        <br /><br />
                                    </>
                                }
                                <Loots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} spell={true}></Loots>
                                <br />
                                <Loots requests={requests} loots={loots} mainName={mainName} isAdmin={isAdmin} spell={false}></Loots>
                            </Col>
                        </Row>
                    }
                </Col>
                {isReady &&
                    <Col md={{ span: 2, offset: 0 }}>
                        <Alert variant='secondary'>
                            Logged in as <strong>{mainName}</strong>
                            <br />
                            <Button onClick={logout}>Logout</Button>
                        </Alert>
                    </Col>
                }
            </Row>
        </Container>
    );
}