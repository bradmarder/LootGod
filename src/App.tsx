import { useState } from 'react';
import './App.css';
import { Container, Row, Col, Alert, Button, Form, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';

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

const api = window.location.protocol + '//' + window.location.hostname + ':3000';
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

    const start = async () => {
        localStorage.setItem('name', mainName);
        setIsReady(true);

        const res = await axios.get<ILootRequest[]>('/GetLootRequests');
        setRequests(res.data);

        await getLoots();
    };
    const getLoots = async () => {
        const res = await axios.get<ILoot[]>('/GetLoots');
        setLoots(res.data);
    };
    const logout = () => {
        localStorage.removeItem('name');
        setMainName('');
        setIsReady(false);
    };

    //useEffect(() => {
    //    (async () => {
    //        await axios.get<ILootRequest>('/user');
    //    })();
    //}, []);
    const isCreateLootDisabled = loots.length === 0 || isLoading || charName === '' || lootId === 0 || eqClass === '';

    const deleteLootRequest = async (id: number) => {
        setIsLoading(true);
        await axios.post(api + '/DeleteLootRequest?id=' + id);
        setRequests(requests.filter(x => x.Id !== id));
        setIsLoading(false);
    };

    const deleteLoot = async (id: number) => {
        setIsLoading(true);
        await axios.post(api + '/DeleteLoot?id=' + id);
        setLoots(loots.filter(x => x.Id !== id));
        setIsLoading(false);
    }

    const createLootRequest = async () => {
        const data = {
            MainName: mainName,
            CharacterName: charName,
            Class: eqClass,
            LootId: lootId,
            Quantity: quantity,
        };
        setIsLoading(true);
        await axios.post(api + '/CreateLootRequest', data);
        setCharName('');
        setLootId(0);
        setQuantity(1);
        setClass('');
        setIsLoading(false);
        // reset form fields?
    };
    const createLoot = async () => {
        const data = {
            Name: createLootName,
            Quantity: createLootQuantity,
        };
        setIsLoading(true);
        await axios.post(api + '/CreateLoot', data);
        setCreateLootName('');
        setCreateLootQuantity(1);
        setIsLoading(false);
    };

    return (
        <Container fluid>
            <Row>
                <Col xs={12} md={3}>
                    <h1>KOI Raid Loot Tool (BETA)</h1>
                    {!isReady &&
                        <Alert variant={'primary'}>
                            <Alert.Heading>Let's Get Started!</Alert.Heading>
                            <p>Please enter your main character name</p>
                            <Form>
                                <Form.Control type="text" value={mainName} onChange={e => setMainName(e.target.value)} />
                            </Form>
                            <br />
                            <Button onClick={start} variant="primary">Start</Button>
                        </Alert>
                    }
                    {isReady &&
                        <>
                            <Row>
                                <Col md={{ span: 3, offset: 9 }}>
                                    <Alert variant='secondary'>
                                        Logged in as <strong>{mainName}</strong>
                                        <br />
                                        <Button onClick={logout}>Logout</Button>
                                    </Alert>
                                </Col>
                            </Row>
                            <Alert variant='primary'>
                                <h2>Create Loot Request</h2>
                                <Form>
                                    <Row>
                                        <Col>
                                            <Form.Group className="mb-3">
                                                <Form.Label>Character Name</Form.Label>
                                                <Form.Control type="text" placeholder="Enter name" />
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
                                                <Form.Select value={lootId} onChange={e => setLootId((e.target as any).value)}>
                                                    <option>Select an Item</option>
                                                    {loots.map(item =>
                                                        <option key={item.Id} value={item.Id}>{item.Name}</option>
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
                            <h2>My Loot Requests</h2>
                            {requests.length === 0 &&
                                <Alert variant='warning'>You currently have zero requests</Alert>
                            }
                            {requests.length > 0 &&
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
                                        {requests.map((item, i) => 
                                            <tr key={item.Id}>
                                                <td>{item.CharacterName}</td>
                                                <td>{item.IsAlt ? 'Alt' : 'Main'}</td>
                                                <td>{item.Class}</td>
                                                <td>{item.LootId}</td>
                                                <td>{item.Quantity}</td>
                                                <td>
                                                    <Button variant='danger' onClick={() => deleteLootRequest(item.Id)}>Delete</Button>
                                                </td>
                                            </tr>
                                        )}
                                    </tbody>
                                </Table>
                            }
                        </>
                    }
                </Col>
                <Col xs={12} md={4}>
                    <Alert variant='primary'>
                        <h2>Create Loot</h2>
                        <Form>
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
                    <h2>Available Loots</h2>
                    {loots.length === 0 &&
                        <Alert variant='warning'>
                            Looks like there aren't any loots available right now
                        </Alert>
                    }
                    {loots.length > 0 &&
                        <Table striped bordered hover>
                            <thead>
                                <tr>
                                    <th>Loot</th>
                                <th>Quantity</th>
                                <th></th>
                                </tr>
                            </thead>
                            <tbody>
                                {loots.map(item =>
                                    <tr key={item.Id}>
                                        <td>{item.Name}</td>
                                        <td>{item.Quantity}</td>
                                        <td>
                                            <Button variant='danger' onClick={() => deleteLoot(item.Id)}>Delete</Button>
                                        </td>
                                    </tr>
                                )}
                            </tbody>
                        </Table>
                    }
                </Col>
            </Row>
        </Container>
    );
}

export default App;

interface ILoot {
    readonly Id: number;
    readonly Name: string;
    readonly Quantity: number;
}
interface ILootRequest {
    readonly Id: number;
    readonly CreatedDate: string;
    readonly MainName: string;
    readonly CharacterName: string;
    readonly Class: EQClass;
    readonly LootId: number;
    readonly Quantity: number;
    readonly IsAlt: boolean;
}
