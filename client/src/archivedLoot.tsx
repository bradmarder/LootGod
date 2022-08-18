import { useState, useEffect, useMemo } from 'react';
import './App.css';
import { Row, Col, Alert, Button, Form, Table } from 'react-bootstrap';
import 'bootstrap/dist/css/bootstrap.min.css';
import axios from 'axios';
import classes from './eqClasses';

const api = process.env.REACT_APP_API_PATH;

export function ArchivedLoot(props: IContext) {

    const [isLoading, setIsLoading] = useState(false);
    const [requests, setRequests] = useState<ILootRequest[]>([]);
    const [name, setName] = useState('');
    const [lootId, setLootId] = useState('');

    const getLootRequests = async () => {
        setIsLoading(true);
        const params = {
            name: name || null,
            lootId: lootId || null,
        };
        const res = await axios.get<ILootRequest[]>(api + '/GetArchivedLootRequests', { params });
        setRequests(res.data);
        setIsLoading(false);
    };

    return (
        <>
            <h3>Archived Loot Requests (Admin Only)</h3>
            <Form onSubmit={e => { e.preventDefault(); }}>
                <Row>
                    <Col>
                        <Form.Group className="mb-3">
                            <Form.Label>Character Name</Form.Label>
                            <Form.Control type="text" placeholder="Enter name" value={name} onChange={e => setName(e.target.value)} />
                        </Form.Group>
                    </Col>
                    <Col>
                        <Form.Group>
                            <Form.Label>Name</Form.Label>
                            <Form.Select value={lootId} onChange={e => setLootId((e.target as any).value)}>
                                <option value=''>Select Loot</option>
                                {props.loots.map((item, i) =>
                                    <option key={item.id} value={item.id}>{item.name}</option>
                                )}
                            </Form.Select>
                        </Form.Group>
                    </Col>
                </Row>
                <Button variant='success' disabled={isLoading} onClick={getLootRequests}>Search</Button>
            </Form>
            {requests.length > 0 &&
                <Table striped bordered hover size="sm">
                    <thead>
                        <tr>
                            <th>Name</th>
                            <th>Alt/Main</th>
                            <th>Class</th>
                            <th>Loot</th>
                            <th>Quantity</th>
                            <th>Upgrading From</th>
                            <th>Granted?</th>
                            <th>Date</th>
                        </tr>
                    </thead>
                    <tbody>
                        {requests.map((item) =>
                            <tr key={item.id}>
                                <td>{item.characterName}</td>
                                <td>{item.isAlt ? 'Alt' : 'Main'}</td>
                                <td>{classes[item.class as any]}</td>
                                <td>{props.loots.find(x => x.id === item.lootId)?.name}</td>
                                <td>{item.spell || item.quantity}</td>
                                <td>{item.currentItem}</td>
                                <td>{item.granted ? "yes" : "no"}</td>
                                <td>{new Date(item.createdDate).toLocaleDateString()}</td>
                            </tr>
                        )}
                    </tbody>
                </Table>
            }
        </>
    );
}