import { useEffect, useState, useMemo } from 'react';
import './App.css';
import { Container, Row, Col, Alert, Button, Form, Table, Accordion } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios, { AxiosPromise } from 'axios';
import { HubConnectionBuilder } from '@microsoft/signalr';

enum EQClass {
    Bard = 'Bard',
    Beastlord = 'Beastlord',
    Beserker = 'Beserker',
    Cleric = 'Cleric',
    Druid = 'Druid',
    Enchanter = 'Enchanter',
    Magician = 'Magician',
    Monk = 'Monk',
    Necromancer = 'Necromancer',
    Paladin = 'Paladin',
    Ranger = 'Ranger',
    Rogue = 'Rogue',
    Shadowknight = 'Shadowknight',
    Shaman = 'Shaman',
    Warrior = 'Warrior',
    Wizard = 'Wizard',
}

const api = window.location.protocol + '//' + window.location.hostname + ':5000';
const name = localStorage.getItem('name');
const classes = [
    EQClass.Bard,
    EQClass.Beastlord,
    EQClass.Beserker,
    EQClass.Cleric,
    EQClass.Druid,
    EQClass.Enchanter,
    EQClass.Magician,
    EQClass.Monk,
    EQClass.Necromancer,
    EQClass.Paladin,
    EQClass.Ranger,
    EQClass.Rogue,
    EQClass.Shadowknight,
    EQClass.Shaman,
    EQClass.Warrior,
    EQClass.Wizard,
];

function App() {
    const [isReady, setIsReady] = useState(name != null);
    const [mainName, setMainName] = useState(name || '');
    const [requests, setRequests] = useState<ILootRequest[]>([]);
    const [loots, setLoots] = useState<ILoot[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [charName, setCharName] = useState('');
    const [quantity, setQuantity] = useState(1);
    const [eqClass, setClass] = useState('');
    const [lootId, setLootId] = useState(0);
    const [createLootName, setCreateLootName] = useState('');
    const [createLootQuantity, setCreateLootQuantity] = useState(1);

    const login = async () => {
        localStorage.setItem('name', mainName);
        await axios.post(api + "/login", { mainName });
        setIsReady(true);
    };
    const getLoots = async () => {
        const res = await axios.get<ILoot[]>(api + '/GetLoots');
        setLoots(res.data);
    };
    const getLootRequests = async () => {
        const res = await axios.get<ILootRequest[]>(api + '/GetLootRequests');
        setRequests(res.data);
    };
    const isAdmin = mainName === 'Benemage' || mainName === 'Dhau';

    useEffect(() => {
        (async () => {
            await getLoots();
            await getLootRequests();
        })();
    }, []);

    useEffect(() => {
        const connection = new HubConnectionBuilder()
            .withUrl(api + "/lootHub")
            .build();

        connection.on("refresh", (loots: ILoot[], requests: ILootRequest[]) => {
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

    const myLootRequests = useMemo(() =>
        requests.filter(x => x.mainName.toLowerCase() === mainName.toLowerCase()),
        [mainName, requests]);

    const isCreateLootDisabled = loots.length === 0 || isLoading || charName === '' || lootId === 0 || eqClass === '' || quantity < 1;

    const deleteLootRequest = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/DeleteLootRequest?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    };

    const deleteLoot = async (id: number) => {
        setIsLoading(true);
        try {
            await axios.post(api + '/DeleteLoot?id=' + id);
        }
        finally {
            setIsLoading(false);
        }
    }

    const createLootRequest = async () => {
        const data = {
            MainName: mainName,
            CharacterName: charName,
            Class: classes.indexOf(eqClass as EQClass),
            LootId: lootId,
            Quantity: quantity,
        };
        setIsLoading(true);
        try {
            await axios.post<{}, AxiosPromise<ILootRequest>>(api + '/CreateLootRequest', data);
        }
        catch {
            // DUPLICATE REQUEST ERROR MESSAGE
            alert('duplicate request for name and loot');
        }
        finally {
            setIsLoading(false);
        }
        setCharName('');
        setLootId(0);
        setQuantity(1);
        setClass('');
    };
    const createLoot = async () => {
        const data = {
            Name: createLootName,
            Quantity: createLootQuantity,
        };
        setIsLoading(true);
        try {
            await axios.post<{}, AxiosPromise<ILoot>>(api + '/CreateLoot', data);
        }
        finally {
            setIsLoading(false);
        }
        setCreateLootName('');
        setCreateLootQuantity(1);
    };
    const spells = loots.filter(x => x.isSpell);
    const lootItems = loots.filter(x => !x.isSpell);
    return (
        <Container fluid>
            <Row>
                <Col xs={12} md={8}>
                    <h1>KOI Raid Loot Tool (BETA)</h1>
                    {!isReady &&
                        <Alert variant={'primary'}>
                            <Alert.Heading>Let's Get Started!</Alert.Heading>
                            <p>Please enter your <strong>MAIN</strong> character name (<em>not</em> your alt/box or "Mickey Mouse")</p>
                            <Form onSubmit={login}>
                                <Form.Control type="text" value={mainName} onChange={e => setMainName(e.target.value)} />
                            </Form>
                            <br />
                            <Button onClick={login} disabled={mainName === ''} variant="primary">Start</Button>
                        </Alert>
                    }
                    {isReady &&
                        <Row>
                            <Col>
                                <Alert variant='primary'>
                                    <h4>Create Loot Request</h4>
                                    <Form onSubmit={e => { e.preventDefault(); }}>
                                        <Row>
                                            <Col>
                                                <Form.Group className="mb-3">
                                                    <Form.Label>Character requesting loot (enter your MAIN's name or your ALT's name)</Form.Label>
                                                    <Form.Control type="text" placeholder="Enter name" value={charName} onChange={e => setCharName(e.target.value)} />
                                                </Form.Group>
                                            </Col>
                                            <Col>
                                                <Form.Group className="mb-3">
                                                    <Form.Label>Class</Form.Label>
                                                    <Form.Select value={eqClass} onChange={e => setClass((e.target as any).value)}>
                                                        <option>Select Class</option>
                                                        {classes.map((item, i) =>
                                                            <option key={item} value={item}>{item}</option>
                                                        )}
                                                    </Form.Select>
                                                </Form.Group>
                                            </Col>
                                        </Row>
                                        <Row>
                                            <Col>
                                                <Form.Group className="mb-3">
                                                    <Form.Label>Loot</Form.Label>
                                                    <Form.Select value={lootId} onChange={e => setLootId(Number((e.target as any).value))}>
                                                        <option>Select an Item</option>
                                                        {loots.map(item =>
                                                            <option key={item.id} value={item.id}>{item.name}</option>
                                                        )}
                                                    </Form.Select>
                                                </Form.Group>
                                            </Col>
                                            <Col>
                                                <Form.Group className="mb-3">
                                                    <Form.Label>Quantity</Form.Label>
                                                    <Form.Control type="number" placeholder="Quantity" min="1" max="255" value={quantity} onChange={e => setQuantity(Number(e.target.value))} />
                                                </Form.Group>
                                            </Col>
                                        </Row>
                                        <Button variant='success' disabled={isCreateLootDisabled} onClick={createLootRequest}>Create</Button>
                                    </Form>
                                </Alert>
                                <br />
                                <h3>My Loot Requests</h3>
                                {myLootRequests.length === 0 &&
                                    <Alert variant='warning'>You currently have zero requests</Alert>
                                }
                                {myLootRequests.length > 0 &&
                                    <Table striped bordered hover size="sm">
                                        <thead>
                                            <tr>
                                                <th>Name</th>
                                                <th>Alt/Main</th>
                                                <th>Class</th>
                                                <th>Loot</th>
                                                <th>Quantity</th>
                                                <th></th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {myLootRequests.map((item, i) =>
                                                <tr key={item.id}>
                                                    <td>{item.characterName}</td>
                                                    <td>{item.isAlt ? 'Alt' : 'Main'}</td>
                                                    <td>{classes[item.class as any]}</td>
                                                    <td>{loots.find(x => x.id === item.lootId)?.name}</td>
                                                    <td>{item.quantity}</td>
                                                    <td>
                                                        <Button variant='danger' onClick={() => deleteLootRequest(item.id)}>Delete</Button>
                                                    </td>
                                                </tr>
                                            )}
                                        </tbody>
                                    </Table>
                                }
                            </Col>
                            <Col>
                                {isAdmin &&
                                    <Alert variant='primary'>
                                        <h4>Create Loot (Admin only)</h4>
                                        <Form onSubmit={createLoot}>
                                            <Row>
                                                <Col>
                                                    <Form.Group>
                                                        <Form.Label>Name</Form.Label>
                                                        <Form.Control type="text" placeholder="Enter loot name" value={createLootName} onChange={e => setCreateLootName(e.target.value)} />
                                                    </Form.Group>
                                                </Col>
                                                <Col>
                                                    <Form.Group>
                                                        <Form.Label>Quantity</Form.Label>
                                                        <Form.Control type="number" placeholder="Quantity" min="1" max="255" value={createLootQuantity} onChange={e => setCreateLootQuantity(Number(e.target.value))} />
                                                    </Form.Group>
                                                </Col>
                                            </Row>
                                            <br />
                                            <Button variant='success' onClick={createLoot}>Create</Button>
                                        </Form>
                                    </Alert>
                                }
                                <h3>Available Spells</h3>
                                {spells.length === 0 &&
                                    <Alert variant='warning'>
                                        Looks like there aren't any spells available right now
                                    </Alert>
                                }
                                {spells.length > 0 &&
                                    <Accordion>
                                        {spells.map((item, i) =>
                                            <Accordion.Item key={item.id} eventKey={i.toString()}>
                                                <Accordion.Header>{item.name} | {item.quantity} available | {requests.filter(x => x.lootId === item.id).length} request(s)</Accordion.Header>
                                                <Accordion.Body>
                                                    {isAdmin &&
                                                        <>
                                                            <Button variant='danger' onClick={() => deleteLoot(item.id)}>Delete "{item.name}" and all {requests.filter(x => x.lootId === item.id).length} request(s)</Button>
                                                            <hr />
                                                        </>
                                                    }
                                                    {requests.filter(x => x.lootId === item.id).map(req =>
                                                        <span key={req.id}><strong>{req.mainName}</strong> | {req.characterName} | {req.isAlt ? 'alt' : 'main'} | {classes[req.class as any]} | {req.quantity}<hr /></span>
                                                    )}
                                                </Accordion.Body>
                                            </Accordion.Item>
                                        )}
                                    </Accordion>
                                }
                                <hr />
                                <h3>Available Loots</h3>
                                {lootItems.length === 0 &&
                                    <Alert variant='warning'>
                                        Looks like there aren't any loots available right now
                                    </Alert>
                                }
                                {lootItems.length > 0 &&
                                    <Accordion>
                                        {lootItems.map((item, i) =>
                                            <Accordion.Item key={item.id} eventKey={i.toString()}>
                                                <Accordion.Header>{item.name} | {item.quantity} available | {requests.filter(x => x.lootId === item.id).length} request(s)</Accordion.Header>
                                                <Accordion.Body>
                                                    {isAdmin &&
                                                        <>
                                                            <Button variant='danger' onClick={() => deleteLoot(item.id)}>Delete "{item.name}" and all {requests.filter(x => x.lootId === item.id).length} request(s)</Button>
                                                            <hr />
                                                        </>
                                                    }
                                                    {requests.filter(x => x.lootId === item.id).map(req =>
                                                        <span key={req.id}><strong>{req.mainName}</strong> | {req.characterName} | {req.isAlt ? 'alt' : 'main'} | {classes[req.class as any]} | {req.quantity}<hr /></span>
                                                    )}
                                                </Accordion.Body>
                                            </Accordion.Item>
                                        )}
                                    </Accordion>
                                }
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

export default App;

interface ILoot {
    readonly id: number;
    readonly name: string;
    readonly quantity: number;
    readonly isSpell: boolean;
}
interface ILootRequest {
    readonly id: number;
    readonly createdDate: string;
    readonly mainName: string;
    readonly characterName: string;
    readonly class: EQClass;
    readonly lootId: number;
    readonly quantity: number;
    readonly isAlt: boolean;
}
